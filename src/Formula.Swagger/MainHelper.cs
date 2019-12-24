using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Formula.Swagger
{
    internal class MainHelper
    {
		public static IEnumerable<Type> GetTypesWithAttribute(Assembly assembly, Type attributeType)
		{
			foreach (Type type in assembly.GetTypes())
			{
				if (type.GetCustomAttributes(attributeType, true).Length > 0)
					yield return type;
			}
		}
		public static List<string> GetSwaggerApiNames(IEnumerable<Assembly> assemblies)
		{
			List<string> swaggerApiNames = new List<string>();
			foreach (Assembly assembly in assemblies)
			{
				IEnumerable<Type> controllerTypes = MainHelper.GetTypesWithAttribute(assembly, typeof(FormulaSwaggerApiAttribute));
				
				foreach (Type controllerType in controllerTypes)
				{
					swaggerApiNames.Add(ApiExplorerGroupFormulaConvention
						.GetGroupNameByControllerType(controllerType.Name));
				}
			}

			return swaggerApiNames;
		}
		public static List<Assembly> GetDefaultSwaggerApiAssemblies()
		{
			return new List<Assembly>()
			{
				Assembly.GetEntryAssembly()
			};
		}

		public static void DeleteFilesAndDirectories(string directoryPath)
		{
			System.IO.DirectoryInfo di = new DirectoryInfo(directoryPath);
			if (di.Exists == false)
				return;

			foreach (FileInfo file in di.GetFiles())
				file.Delete();
			foreach (DirectoryInfo dir in di.GetDirectories())
				dir.Delete(true);
		}

		public static void DeleteDirectory(string directoryPath)
		{
			System.IO.DirectoryInfo di = new DirectoryInfo(directoryPath);
			if (di.Exists == false)
				return;
			di.Delete(true);
		}

		public static HashSet<string> GetClientDirectoryNames()
		{
			var clientDirectories = new HashSet<string>();
			System.IO.DirectoryInfo di = new DirectoryInfo(Path.Combine("webobjects", "clients"));
			if (di.Exists == false)
				return clientDirectories;

			foreach (DirectoryInfo dir in di.GetDirectories())
				clientDirectories.Add(dir.Name.ToLowerInvariant());
			return clientDirectories;
		}
	}
}
