using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Formula.Swagger
{
    public class ApiExplorerGroupFormulaConvention : IControllerModelConvention
	{
		public void Apply(ControllerModel controller)
		{
			string groupName = GetGroupNameByControllerType(controller.ControllerType.Name);
			controller.ApiExplorer.GroupName = groupName.ToLower();
		}

		public static string GetGroupNameByControllerType(string controllerName)
		{
			return string.Join("", controllerName.SkipLast(10));
		}
	}
}
