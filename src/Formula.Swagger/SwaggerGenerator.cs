using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSwag;
using NSwag.CodeGeneration.TypeScript;
using NSwag.Generation;

namespace Formula.Swagger
{
    public static class SwaggerGenerator
    {
        static ILogger<SwaggerGeneratorClass> _logger;
        public class SwaggerGeneratorClass { }
        public static async Task GenerateSwagger(this IHost host)
        {
            _logger = host.Services.GetRequiredService<ILogger<SwaggerGeneratorClass>>();

            await CreateSwaggerFiles(host);
            await CreateSwaggerClients(host);
        }

        static async Task CreateSwaggerFiles(IHost host)
        {
            var swaggerGenerator = host.Services.GetRequiredService<IOpenApiDocumentGenerator>();

            if (Directory.Exists(Path.Combine("wwwroot", "swaggerdocs")))
                Directory.Delete(Path.Combine("wwwroot", "swaggerdocs"), true);

            var apiTypes = SwaggerHelper.GetControllerTypes();
            foreach (Type apiType in apiTypes)
            {
                var openApiDoc = await swaggerGenerator.GenerateAsync(
                    SwaggerHelper.GetDocumentNameOfController(apiType));
                string documentName = SwaggerHelper.GetDocumentNameOfController(apiType);
                string filePath = Path.Combine("wwwroot", "swaggerdocs", $"{documentName}.swagger.json");
                var upserStatus = await UpsertFileIfDifferent(filePath, openApiDoc.ToJson());
                _logger.LogInformation($"Upserted file '{filePath}' - {upserStatus}");
            }
        }

        static async Task CreateSwaggerClients(IHost host)
        {
            //if (Directory.Exists(Path.Combine("JsLibs", "Clients")))
            //    Directory.Delete(Path.Combine("JsLibs", "Clients"), true);
            HashSet<string> currentClientFiles = new HashSet<string>();

            string[] swaggerDocs = Directory.GetFiles(Path.Combine("wwwroot", "swaggerdocs"));
            foreach (string swaggerDoc in swaggerDocs)
            {
                var openApiDoc = await OpenApiDocument.FromJsonAsync(File.ReadAllText(swaggerDoc));
                currentClientFiles.Add(await CreateSwaggerClient(openApiDoc));
            }

            HashSet<string> pastClientFiles = Directory.GetFiles(Path.Combine("JsLibs", "Clients"), "*.*", SearchOption.AllDirectories).ToHashSet();
			foreach (string pastClientFile in pastClientFiles)
			{
                if (currentClientFiles.Contains(pastClientFile) == false)
                    File.Delete(pastClientFile);
			}
        }

        static async Task<string> CreateSwaggerClient(OpenApiDocument openApidocument)
        {
            string name = openApidocument.Info.Title;
            var settings = new TypeScriptClientGeneratorSettings
            {
                ClassName = $"{name}Client",
                TypeScriptGeneratorSettings =
                {
                },
            };
            var generator = new TypeScriptClientGenerator(openApidocument, settings);
            var code = generator.GenerateFile();
            code = code.Replace("this.baseUrl = baseUrl ? baseUrl : \"/\";", "this.baseUrl = baseUrl ? baseUrl : \"\";");

            string filePath = Path.Combine("JsLibs", "Clients", $"{name}Client.ts");
            var status = await UpsertFileIfDifferent(filePath, code);
            _logger.LogInformation("Swagger client generation: {filePath}. Status: {status}", filePath, status);
            return filePath;
        }

        private static async Task<UpsertStatus> UpsertFileIfDifferent(string filePath, string content)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filePath);
            if (file.Exists)
            {
                string currentContent = await System.IO.File.ReadAllTextAsync(file.FullName);
                if (currentContent != content)
                {
                    await File.WriteAllTextAsync(file.FullName, content, Encoding.UTF8);
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

        enum UpsertStatus
        {
            Created, Updated, SameContentExist
        }
    }
}
