using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Formula
{
	internal static class FormulaPathHelper
    {
		/// <summary>
		/// FullName: "Contoso.Pages.Account.Settings.SettingsPage"
		/// Returns: "Account.Settings".
		/// </summary>
		public static string PageName(Type pageType)
		{
			Match match = Regex.Match(pageType.Namespace, @"^.+\.Pages\.(.+)");
			return match.Groups[1].Value;
		}
		/// <summary>
		/// FullName: "Contoso.Layouts.Main.MainLayout"
		/// Returns: "Main"
		/// </summary>
		public static string LayoutName(Type layoutType)
		{
			return layoutType.Namespace.Split(".").Last();
		}
		
		/// <summary>
		/// Fullname: Contoso.Pages.Account.Settings
		/// Returns: /Pages/Account/Settings/Settings.cshtml
		/// </summary>
		/// <param name="type">The type of the class</param>
		/// <param name="isTypeInOwnFolder">If true, then it will append its own folder. 
		/// Eg if the types fullname is Contoso.Pages.Account.Settings, then it will return 
		/// /Pages/Account/Settings/Settings.cshtml, if false, then: /Pages/Account/Settings.cshtml</param>
		/// <returns></returns>
		public static string View(Type type, bool withExtension = false)
		{
			return GetFilePathByConvention(type, withExtension ? "cshtml" : null);
		}
		private static string GetFilePathByConvention(Type type, string fileExt)
		{
			//TODO: Delete this function, only cshtml is useing this
			string[] pathLevels = type.FullName.Split('.'); // ["Contoso", "Pages", "Foo", "FooPage"]
			StringBuilder sb = new StringBuilder();
			for (int i = 1; i < pathLevels.Length; i++)
				sb.Append("/" + pathLevels[i]);
			sb.Append((fileExt == null ? "" : "." + fileExt));
			return sb.ToString();
		}
		
		public static bool IsTypeALayout(Type layoutOrPageType)
		{
			string[] splittedFullName = layoutOrPageType.FullName.Split('.');
			if (splittedFullName[1] == "Pages")
				return false;
			else if (splittedFullName[1] == "Layouts")
				return true;
			throw new System.ArgumentException("The type is neither a Page or Layout");
		}

		public static string GetAppDir()
		{
			return "app";
		}
		public static string GetLayoutDir(Type layoutType)
		{
			return Path.Combine("layouts", layoutType.Namespace.Split(".").Last());
		}
		public static string GetPageDir(Type pageType)
		{
			string fullName = pageType.Namespace; // Contoso.Pages.Account
			string[] nameItems = fullName.Split('.'); // ["Contoso", "Pages", "Account"]
			return Path.Combine(nameItems.Skip(1).ToArray());
		}

		public static Type GetTypeByWebObjectName(string fullName)
		{
			//If "Menus.TopMenu" then FullName is "<DefaultNamespace>.WebObjects.Menus.TopMenu.TopMenuWebObject"
			//If "Calendar" then FullName is "<DefaultNamespace>.WebObjects.Calendar.CalendarWebObject"

			var name = fullName.Split('.').Last();

			Assembly assembly = Assembly.GetEntryAssembly();
			return assembly.GetType($"{assembly.GetName().Name}.WebObjects.{fullName}.{name}");
		}
	}
}
