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
			return pageType.Namespace.Substring(FormulaConfig.DefaultNamespace.Length + 1 + "Pages.".Length);
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
		/// Fullname: Contoso.Web.Pages.Account.Settings.SettingsPage
		/// Returns: /Pages/Account/Settings/SettingsPage.cshtml
		/// </summary>
		/// <param name="type">The type of the class</param>
		public static string View(Type type, bool withExtension = false)
		{
			return GetFilePathByConvention(type, withExtension ? "cshtml" : null);
		}
		private static string GetFilePathByConvention(Type type, string fileExt)
		{
			string t = type.FullName.Substring(FormulaConfig.DefaultNamespace.Length + 1); //Pages.Account.Settings.SettingsPage
			t = $"/{t.Replace('.', '/')}";
			if (string.IsNullOrWhiteSpace(fileExt) == false)
				t = t + ".cshtml";
			return t;
		}
		
		public static bool IsTypeALayout(Type layoutOrPageType)
		{
			string t = layoutOrPageType.FullName.Substring(FormulaConfig.DefaultNamespace.Length + 1); //Pages.Account.Settings.SettingsPage
			string t2 = t.Split('.')[0];
			if (t2 == "Pages")
				return false;
			else if (t2 == "Layouts")
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
			string t = pageType.Namespace.Substring(FormulaConfig.DefaultNamespace.Length + 1); //Pages.Account.Settings.SettingsPage
			return Path.Combine(t.Split('.'));
		}

		public static Type GetTypeByWebObjectName(string webObjName)
		{
			//webObjName eg: Menus.TopMenu

			//If "Menus.TopMenu" then FullName is "<DefaultNamespace>.WebObjects.Menus.TopMenu.TopMenuWebObject"
			//If "Calendar" then FullName is "<DefaultNamespace>.WebObjects.Calendar.CalendarWebObject"

			var lastNamePart = webObjName.Split('.').Last();

			string typeName = $"{FormulaConfig.DefaultNamespace}.WebObjects.{webObjName}.{lastNamePart}WebObject";

			Assembly assembly = Assembly.GetEntryAssembly();
			return assembly.GetType(typeName);
		}
	}
}
