using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Formula.Swagger
{
    public class ApiExplorerGroupFormulaConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller)
        {
            controller.ApiExplorer.GroupName = controller.ControllerType.Name;
        }
    }
}
