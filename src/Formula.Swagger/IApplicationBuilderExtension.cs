using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;

namespace Formula.Swagger
{
    public static class IApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseFormulaSwaggerUi(this IApplicationBuilder app, List<Assembly> swaggerApiAssemblies = null)
		{
			swaggerApiAssemblies ??= MainHelper.GetDefaultSwaggerApiAssemblies();

			var apiNames = MainHelper.GetSwaggerApiNames(swaggerApiAssemblies);
			app.UseSwaggerUI(c =>
			{
				foreach (string apiName in apiNames)
				{
					//Here, we specify the swaggers json files we generated
					//Its in our, eg: wwwroot/swaggerdocs/bar.swagger.json
					c.SwaggerEndpoint($"/swaggerdocs/{apiName.ToLowerInvariant()}.swagger.json", apiName);
				}
			});
			return app;
		}
    }
}
