using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace Formula.Swagger
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseFormulaSwagger(this IApplicationBuilder app, List<Assembly> swaggerApiAssemblies = null)
		{
			app.UseSwaggerUi3(x =>
			{
				//Add if you wanna override defaults and show whats from swagger.json files
				string[] files = Directory.GetFiles(Path.Combine("wwwroot", "swaggerdocs"));
				foreach (string file in files)
				{
					var fileName = file.Split(Path.DirectorySeparatorChar)[^1];
					fileName = fileName.Substring(0, fileName.Length - ".swagger.json".Length); //hello-world
					x.SwaggerRoutes.Add(new NSwag.AspNetCore.SwaggerUi3Route(fileName, $"/swaggerdocs/{fileName}.swagger.json"));
				}
			});

			return app;
		}
    }
}
