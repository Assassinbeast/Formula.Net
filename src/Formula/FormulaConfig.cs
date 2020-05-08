using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Formula
{
	public class FormulaConfigArg
	{
		/// <summary>
		/// The assembly where you Pages are located
		/// </summary>
		public Assembly AppAssembly;
		/// <summary>
		/// The default layout your pages will be using
		/// </summary>
		public Type DefaultLayout;
		/// <summary>
		/// Default value is the name of the Project. In code: Assembly.GetEntryAssembly().GetName().Name
		/// </summary>
		public string BaseTitle;
		/// <summary>
		/// The default FullTitleGenerationFunc will be a function that will return 
		/// the title of the BasePageEnd viewcontroller, and then
		/// with a dash symbol and then the BaseTitle. 
		/// For example, if you BaseTitle is "Contoso", and your BasePageEnd returns "About", 
		/// Then this Func will return "About - Contoso". If your BasePageEnd returns null or empty,
		/// (probably the homepage) then this Func will return "Contoso".
		/// You can override this Func, the argument will be the Title of the BasePageEnd ViewController.
		/// </summary>
		public Func<string, string> FullTitleGenerationFunc;
		/// <summary>
		/// Your own Cdn. If your webapp is called Contoso, and you are using azure Cdn,
		/// then your cdn root path to your files could be https://contoso.azureedge.net/formulafiles .
		/// Then if you have a localfile in wwwroot/app/app.js, then your cdn file should be under
		/// https://contoso.azureedge.net/formulafiles/app/app.js
		/// The Formula framework will try to download the file from the Cdn first, and if it
		/// fails, it will try to download it from your local server under the wwwroot.
		/// When developing, you might not want to use your Cdn (because it might not be up-to-date when you build with gulp)
		/// So a good tip to set the value could is something like: 
		/// Cdn = env.IsDevelopment() ? null : "https://contoso.azureedge.net/formulafiles"
		/// </summary>
		public string Cdn;
		/// <summary>
		/// The AppVersion of your webapp.
		/// Used if you want to update you webapplication and publish it.
		/// Then if some clients have an older version, eg. version 1, and you updated it
		/// to version 2, then the client will automatically make a full page refresh when trying
		/// to dynamically load with SPA functionality.
		/// </summary>
		public int AppVersion;
		/// <summary>
		/// Default value is 3.
		/// This will set the limit on how many internal redirect you are allowed to make, 
		/// in the LogicAction functions of your ViewControllers, with return LogicAction.Redirect(...).
		/// If it exceeds the maximum limit, then the webapp will throw an exception.
		/// </summary>
		public int? MaxInternalRedirectCount;
		/// <summary>
		/// If true, and you update a css/js file, then the new files wont be served
		/// unless you restart the dotnet webapp..
		/// The default value is false when dotnet environment is in "Development", otherwise its true.
		/// </summary>
		public bool? CacheJsAndCss;

		public string DefaultNamespace { get; set; }


		/// <param name="appAssembly">Specify where your Formula Pages in your app are located</param>
		/// <param name="appVersion">The AppVersion when publishing to production. 
		/// You can put in eg. 1, and then 2 for the next release.
		/// Its used to make sure all clients are up-to-date, for example if 
		/// a client is browsing your webapp, and then you publish a new version with another appVersion,
		/// then the client will make a full page reload, instead of dynamically reloading,
		/// because the client be up-to-date with the latest js/css etc.</param>
		/// <param name="defaultLayout">Specify the default Layout that pages will use as a Layout.
		/// Its required even if you only have one Layout</param>
		/// <param name="getPageCallback">Specify your callback function that will set a page to draw</param>
		public FormulaConfigArg(Assembly appAssembly, int appVersion, Type defaultLayout, string defaultNamespace)
		{
			if (appAssembly == null)
				throw new ArgumentException($"{nameof(appAssembly)} is null");
			if (defaultLayout == null)
				throw new ArgumentException($"{nameof(defaultLayout)} is null");

			this.AppAssembly = appAssembly;
			this.AppVersion = appVersion;
			this.DefaultLayout = defaultLayout;
			this.DefaultNamespace = defaultNamespace;
		}
	}

	public static class FormulaConfig
	{
		public static Assembly AppAssembly { get; private set; }
		public static int AppVersion { get; private set; }
		public static Type AppType { get; private set; }
		public static Type RootPageType{ get; private set; }
		public static Type DefaultLayoutType { get; private set; }
		public static string BaseTitle { get; private set; }
		public static string Cdn { get; private set; }
		public static int MaxRedirectCount { get; private set; }
		public static bool CacheJsAndCss { get; private set; }
		public static Func<string, string> FullTitleGenerationFunc { get; private set; }
		/// <summary>
		/// Eg Contoso.Web
		/// </summary>
		public static string DefaultNamespace { get; set; }

		public static void Initialize(IWebHostEnvironment env, FormulaConfigArg config)
		{
			if (config.DefaultLayout.IsSubclassOf(typeof(BaseLayout)) == false ||
				config.DefaultLayout.IsAbstract)
				throw new System.ArgumentException("FormulaConfigArg.DefaultLayout must inherit from BaseLayout and not be abstract");

			AppAssembly = config.AppAssembly;
			AppVersion = config.AppVersion;
			AppType = GetAppType(config.AppAssembly);
			DefaultLayoutType = config.DefaultLayout;
			BaseTitle = config.BaseTitle ?? Assembly.GetEntryAssembly().GetName().Name;
			Cdn = config.Cdn?.TrimEnd('/');
			MaxRedirectCount = config.MaxInternalRedirectCount ?? 3;
			CacheJsAndCss = config.CacheJsAndCss ?? !env.IsDevelopment();
			FullTitleGenerationFunc = config.FullTitleGenerationFunc ?? StandardFunctions.StandardFullTitleGenerationFunc;
			DefaultNamespace = config.DefaultNamespace;

		}
		static Type GetAppType(Assembly assembly)
		{
			Type[] baseAppTypes = assembly
				.GetTypes()
				.Where(x => x.IsSubclassOf(typeof(BaseApp)) && !x.IsAbstract)
				.ToArray();

			if (baseAppTypes.Length != 1)
				throw new System.ArgumentException("There must be only be one BaseApp");
			return baseAppTypes[0];
		}
	}
}
