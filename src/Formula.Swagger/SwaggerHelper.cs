using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Formula.Swagger
{
    internal static class SwaggerHelper
    {
        const string PostName = "Controller";

        public static Type[] GetControllerTypes()
        {
            return Assembly.GetEntryAssembly().GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(ControllerBase)))
                .ToArray();
        }
        public static string GetDocumentNameOfController(Type controllerType)
        {
            //If Controller name is HelloWorldController, then documentname must be hello-world

            //Turns name 'HelloWorldController' into 'HelloWorld'
            string name = controllerType.Name.Substring(0, controllerType.Name.Length - PostName.Length);

            StringBuilder sb = new StringBuilder(name);
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                if (char.IsUpper(sb[i]))
                    sb.Insert(i, '-');
            }
            name = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries)).ToLower();
            return name;
        }
        public static string GetControllerNameOfController(Type type)
        {
            //Turns name 'HelloWorldController' into 'HelloWorld'
            string name = type.Name.Substring(0, type.Name.Length - PostName.Length);

            return name;
        }
    }
}
