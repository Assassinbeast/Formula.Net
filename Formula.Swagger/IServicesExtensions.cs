using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Formula.Swagger
{
    public static class IServicesExtensions
    {
		/// <summary>
		/// 
		/// </summary>
		/// <param name="service"></param>
		/// <param name="swaggerApiAssemblies">All the assemblies where the swagger apis are located. 
		/// If null, it will only generate swagger apis from the Assembly.GetEntryAssembly()</param>
		/// <returns></returns>
        public static IServiceCollection AddFormulaSwaggerGen(this IServiceCollection service, IEnumerable<Assembly> swaggerApiAssemblies = null)
		{
			swaggerApiAssemblies = swaggerApiAssemblies == null ? MainHelper.GetDefaultSwaggerApiAssemblies() : swaggerApiAssemblies;

			var apiNames = MainHelper.GetSwaggerApiNames(swaggerApiAssemblies);

			service.AddSwaggerGen((c) =>
			{
				foreach (string apiName in apiNames)
				{
					c.SwaggerDoc(apiName.ToLowerInvariant(), new OpenApiInfo { Title = apiName + " - API", Version = "v1" });
				}
			});
			return service;
		}


	}
}
