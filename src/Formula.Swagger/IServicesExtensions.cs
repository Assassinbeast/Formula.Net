using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Formula.Swagger
{
    public static class IServicesExtensions
    {
		public static void AddFormulaOpenApiDocuments(this IServiceCollection services)
		{
			var controllerTypes = SwaggerHelper.GetControllerTypes();
			foreach (var type in controllerTypes)
			{
				string controllerName = SwaggerHelper.GetControllerNameOfController(type);
				services.AddOpenApiDocument(x =>
				{
					x.DocumentName = SwaggerHelper.GetDocumentNameOfController(type);
					x.ApiGroupNames = new string[] { type.Name };
					x.PostProcess = process =>
					{
						process.Info.Version = "v1";
						process.Info.Title = controllerName;
						process.Info.Description = $"{controllerName} API";
						foreach (var operation in process.Operations)
						{
							operation.Operation.OperationId = string.Join('_',
								operation.Operation.OperationId.Split('_', StringSplitOptions.RemoveEmptyEntries).Skip(1));
						}
					};
					x.UseRouteNameAsOperationId = false;
				});
			}
		}
	}
}
