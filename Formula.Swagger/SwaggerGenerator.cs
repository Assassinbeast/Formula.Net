using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using NSwag.CodeGeneration.TypeScript;
using Swashbuckle.AspNetCore.Swagger;

namespace Formula.Swagger
{
	public static class SwaggerGenerator
	{
		// ReSharper disable once ClassNeverInstantiated.Local
		public class SwaggerGeneratorClass { }
		static IWebHostEnvironment _env;
		static ISwaggerProvider _swaggerProvider;
		static ILogger<SwaggerGeneratorClass> _logger;
		public static async Task GenerateSwagger(this IHost host, List<Assembly> swaggerApiAssemblies = null)
		{
			_env = (IWebHostEnvironment)host.Services.GetService(typeof(IWebHostEnvironment));
			_swaggerProvider = (ISwaggerProvider)host.Services.GetService(typeof(ISwaggerProvider));
			_logger = (ILogger<SwaggerGeneratorClass>)host.Services.GetService(typeof(ILogger<SwaggerGeneratorClass>));

			if (_env.IsDevelopment() == false)
				return;

			swaggerApiAssemblies ??= MainHelper.GetDefaultSwaggerApiAssemblies();
			var currentDistApiNames = MainHelper.GetClientDirectoryNames();
			var newApiNames = MainHelper.GetSwaggerApiNames(swaggerApiAssemblies);
			foreach (var newApiName in newApiNames)
				if(currentDistApiNames.Contains(newApiName.ToLowerInvariant()) == false)
					MainHelper.DeleteDirectory(Path.Combine("WebObjects", "Clients", newApiName));

			_logger.LogInformation("Upserting Swagger files");
			foreach (string apiName in newApiNames)
			{
				var swaggerDoc = await CreateSwaggerDocument(host, apiName.ToLowerInvariant());
				await CreateSwaggerJson(swaggerDoc, apiName);
				await CreateTypescriptClient(swaggerDoc, apiName);
			}
		}

		static async Task<NSwag.OpenApiDocument> CreateSwaggerDocument(IHost host, string module)
		{
			var doc = _swaggerProvider.GetSwagger(module, null, "/");
			doc.Servers.Clear();
			var jsonDoc = doc.Serialize(OpenApiSpecVersion.OpenApi3_0, OpenApiFormat.Json);
			
			var nSwagDocument = await NSwag.OpenApiDocument.FromJsonAsync(jsonDoc);
			return nSwagDocument;
		}
		static async Task CreateTypescriptClient(NSwag.OpenApiDocument swaggerDocument, string module)
		{
			var settings = new TypeScriptClientGeneratorSettings
			{
				ClassName = $"{module}Client",
				TypeScriptGeneratorSettings =
				{
					Namespace = $"WebObjects.Clients.{module}"
				}
			};
			var generator = new TypeScriptClientGenerator(swaggerDocument, settings);
			var code = generator.GenerateFile();
			code = code.Replace("this.baseUrl = baseUrl ? baseUrl : \"/\";", "this.baseUrl = baseUrl ? baseUrl : \"\";");
			code = CreateWebObjectCode(module) + "\n" + code;
			string filePath = Path.Combine("WebObjects", "Clients", module, $"{module}WebObject.ts");
			var status = await UpsertFileIfDifferent(filePath, code);
			_logger.LogInformation("Swagger client generation: {filePath}. Status: {status}", filePath , status);
		}
		static async Task CreateSwaggerJson(NSwag.OpenApiDocument swaggerDocument, string module)
		{
			string filePath = Path.Combine("wwwroot", "swaggerdocs", $"{module.ToLowerInvariant()}.swagger.json");
			await UpsertFileIfDifferent(filePath, swaggerDocument.ToJson());
		}
		private static async Task<UpsertStatus> UpsertFileIfDifferent(string filePath, string content)
		{
			System.IO.FileInfo file = new System.IO.FileInfo(filePath);
			if(file.Exists)
			{
				string currentContent = await System.IO.File.ReadAllTextAsync(file.FullName);
				if (currentContent != content)
				{
					await System.IO.File.WriteAllTextAsync(file.FullName, content, Encoding.UTF8);
					return UpsertStatus.Updated;
				}
				else
					return UpsertStatus.SameContentExist;
			}
			else
			{
				file.Directory.Create(); // Creates the directories it misses
				await System.IO.File.WriteAllTextAsync(file.FullName, content, Encoding.UTF8);
				return UpsertStatus.Created;
			}
		}
		
		static string CreateWebObjectCode(string module)
		{
			return $"namespace WebObjects.Clients.{module} {{ export class {module}WebObject extends ff.WebObject {{ }} }}";
		}

		enum UpsertStatus
		{
			Created, Updated, SameContentExist
		}
	}
}
