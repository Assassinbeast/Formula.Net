using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Formula.Helpers;
using Formula.Tools;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Formula
{
	public class FormulaContext
	{
		public HttpContext HttpContext;

		/// <summary>
		/// Starts with '/' and doesn't end with '/'
		/// </summary>
		public string Path; 
		public bool IsFirstPageLoad;
		internal string DrawnClientLayout;
		internal string[] DrawnClientPages;
		internal HashSet<string> DrawnWebObjects; //Eg: "[menu.sidemenu]"

		internal bool IsClientPageDrawn(Type pageType)
		{
			if (this.DrawnClientPages == null) //Null if first-page-load
				return false;
			return this.DrawnClientPages.Contains(FormulaPathHelper.PageName(pageType));
		}
		internal bool IsClientLayoutDrawn(Type layoutType)
		{
			return this.DrawnClientLayout == FormulaPathHelper.LayoutName(layoutType);
		}
		internal void SetTargetFfFolder(FfFolderType folderType, string rcTargetFolderPageName)
		{
			if (this.IsFirstPageLoad)
				return; //No need for this header
			this.PageData["ff_targetfoldertype"] = folderType.ToString().ToLowerInvariant();
			this.PageData["ff_targetfolderpagename"] = rcTargetFolderPageName;
		}

		public string Title => FinalPage.GenerateFullTitle();

		public BaseApp App;
		public BaseLayout Layout;
		public List<BasePage> Pages = new List<BasePage>();
		public BasePage FinalPage => Pages[^1];

		#region TypeRefs
		Dictionary<Type, object> typeRefs = new Dictionary<Type, object>();
		public void SetTypeRef(object obj)
		{
			this.typeRefs[obj.GetType()] = obj;
		}
		public T GetTypeRef<T>() where T : BasePage
		{
			return (T)this.typeRefs[typeof(T)];
		}
		public object GetTypeRef(Type type)
		{
			return this.typeRefs[type];
		}
		#endregion

		internal Dictionary<string, ScriptItem> Scripts = new Dictionary<string, ScriptItem>();
		internal List<string> WebObjectStyles = new List<string>();
		internal void AddScriptsFromDir(string dirWithoutWwwroot, bool includeSubDir)
		{
			Dictionary<string, ScriptItem> scriptItemsDic = ScriptItem.GenerateScriptItemsFromDir(dirWithoutWwwroot, includeSubDir);
			foreach (var item in scriptItemsDic)
				this.Scripts.TryAdd(item.Key, item.Value);
		}
		internal void AddWebObjectStylesFromDir(string dir, string webobjectName)
		{
			List<string> styleItems = StyleTool.GetStyleElementStrings(dir, false, webobjectName);
			this.WebObjectStyles.AddRange(styleItems);
		}
		
		internal void Initialize(HttpContext context, string path, 
			bool isFirstPageLoad, string drawnClientLayout, string[] drawnClientPages, HashSet<string> drawnWebObjects)
		{
			this.HttpContext = context;
			this.Path = path;
			this.IsFirstPageLoad = isFirstPageLoad;
			this.DrawnClientLayout = drawnClientLayout;
			this.DrawnClientPages = drawnClientPages;
			this.DrawnWebObjects = drawnWebObjects;
		}

		internal Dictionary<string, object> PageData = new Dictionary<string, object>();
		internal string GetPageData()
		{
			this.PageData.Add("ff_scripts", this.Scripts.Select(x=>x.Value).ToList());

			if (this.IsFirstPageLoad == true)
			{
				this.PageData.Add("ff_polyfills", Utility.Polyfill.GetPolyfills(this));
			}
			else // SPA load
			{
				this.PageData.Add("ff_title", this.Title);
				//If First Page load, then it will be set in the html in the #ff-webobject-styles element in the DrawProcessor.cs
				this.PageData.Add("ff_webobjectstyles", this.WebObjectStyles); 
			}

			string x = JsonConvert.SerializeObject(PageData, EngineItems.JsonSettings);
			return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(x));
		}
		public void AddPageData(string key, object value)
		{
			this.PageData.Add(key, value);
		}
		public string GetAppData()
		{
			string x = JsonConvert.SerializeObject(FormulaConfig.AppData);
			return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(x));
		}
	}

	internal enum FfFolderType
	{
		App, Layout, Page
	}

	public class ScriptItem
	{
		public string Path;
		public bool IsModule;
		static readonly string _systemJsModulesDir = $"{System.IO.Path.DirectorySeparatorChar}jsmodules";
		static readonly string _systemWwwrootDir = $"{System.IO.Path.DirectorySeparatorChar}wwwroot";

		internal static Dictionary<string, ScriptItem> GenerateScriptItemsFromDir(string dirWithoutWwwroot, bool includeSubDir)
		{
			Dictionary<string, ScriptItem> scriptItemDic = new Dictionary<string, ScriptItem>();
			string systemDirWithoutWwwroot = PathHelper.ToSystemPath(dirWithoutWwwroot); // \webobjects\foo

			string systemDirWithWwwroot = $"{_systemWwwrootDir}{systemDirWithoutWwwroot}"; // \wwwroot\webobjects\foo
			var jsSystemFilesWithWwwroot = FileHelper.GetFilesInDirectory(systemDirWithWwwroot, "js", includeSubDir); //["\wwwroot\webobjects\foo\foowebobject.js"]

			foreach (string jsSystemFileWithWwwroot in jsSystemFilesWithWwwroot)
				RecursiveAddJsFile(scriptItemDic, jsSystemFileWithWwwroot);

			return scriptItemDic;
		}

		internal static void RecursiveAddJsFile(Dictionary<string, ScriptItem> scriptItemDic, string jsSystemFileWithWwwroot)
		{
			jsSystemFileWithWwwroot = System.IO.Path.DirectorySeparatorChar + jsSystemFileWithWwwroot.TrimStart(System.IO.Path.DirectorySeparatorChar);

			var scriptItem = new ScriptItem();

			scriptItem.Path = PathHelper.ToUrlPath(jsSystemFileWithWwwroot.Substring(_systemWwwrootDir.Length));

			var fileNameWithoutExt = MiscHelper.GetFileNameWithoutExtension(jsSystemFileWithWwwroot);
			scriptItemDic.TryAdd(scriptItem.Path, scriptItem);

			//If JsModule, then recursive add
			if (CheckIfFileIsJsModule(jsSystemFileWithWwwroot) == true)
			{
				scriptItem.IsModule = true;
				string[] dependencies = GetDependenciesByJsModuleFile(jsSystemFileWithWwwroot); //["/webobjects/pizza/pizzawebobject.js", "/pages/bar/barpage.js"]
				foreach (string dependencyUrlFilePath in dependencies)
				{
					string dependencyUrlFilePath2 = $"/{dependencyUrlFilePath.TrimStart('/')}"; // /webobjects/pizza/pizzawebobject.js
					if (scriptItemDic.ContainsKey(dependencyUrlFilePath2))
						continue;

					string systemFilePath = PathHelper.ToSystemPath(dependencyUrlFilePath2);
					string[] pathItems = systemFilePath.Split(System.IO.Path.DirectorySeparatorChar);
					string systemDependencyPath = System.IO.Path.DirectorySeparatorChar +
						System.IO.Path.Combine(pathItems.Prepend("wwwroot").ToArray()).TrimStart(System.IO.Path.DirectorySeparatorChar); // \wwwroot\webobjects\pizza\pizzawebobject.js

					RecursiveAddJsFile(scriptItemDic, systemDependencyPath);
				}
			}
		}

		internal static bool CheckIfFileIsJsModule(string systemfilePathWithWwwRoot)
		{
			//systemfilePathWithWwwRoot is eg: \wwwroot\webobjects\foo\foowebobject.js
			//we now have to find if its dependencies file exist in \JsModules\webobjects\foo\foowebobject.dependencies.js

			string systemFile = _systemJsModulesDir +
								systemfilePathWithWwwRoot.Substring(
								_systemWwwrootDir.Length, 
								systemfilePathWithWwwRoot.Length - _systemWwwrootDir.Length - 2) + 
								"dependencies.js";
			var fileInfo = FileHelper.GetFileInfo(systemFile);
			return fileInfo != null;
		}
		internal static string[] GetDependenciesByJsModuleFile(string systemFilePathWithWwwRoot)
		{
			//Returns an array of 

			//systemfilePathWithWwwRoot is eg: \wwwroot\webobjects\foo\foowebobject.js
			//we now have to find if its dependencies file exist in \JsModules\webobjects\foo\foowebobject.dependencies.js

			string systemJsDependencyFile = _systemJsModulesDir +
					systemFilePathWithWwwRoot.Substring(
					_systemWwwrootDir.Length,
					systemFilePathWithWwwRoot.Length - _systemWwwrootDir.Length - 2) +
					"dependencies.js";

			string dependencyFileArrayJson;
			using (Stream stream = FileHelper.GetFileInfo(systemJsDependencyFile).CreateReadStream())
			{
				var streamReader = new StreamReader(stream);
				dependencyFileArrayJson = streamReader.ReadToEnd();
			}

			string[] dependencyFileArray = JsonConvert.DeserializeObject<string[]>(dependencyFileArrayJson);

			return dependencyFileArray;
		}
	}
}
