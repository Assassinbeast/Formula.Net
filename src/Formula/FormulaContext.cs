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
		/// Starts with '/' and doesn't with with '/'
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
			return this.DrawnClientPages.Contains(PathHelper.PageName(pageType));
		}
		internal bool IsClientLayoutDrawn(Type layoutType)
		{
			return this.DrawnClientLayout == PathHelper.LayoutName(layoutType);
		}
		internal void SetTargetFfFolder(FfFolderType folderType, string rcTargetFolderPageName)
		{
			if (this.IsFirstPageLoad)
				return; //No need for this header
			this.RData["ff_targetfoldertype"] = folderType.ToString().ToLowerInvariant();
			this.RData["ff_targetfolderpagename"] = rcTargetFolderPageName;
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

		internal List<ScriptItem> Scripts = new List<ScriptItem>();
		internal List<string> WebObjectStyles = new List<string>();
		internal void AddScriptsFromDir(string dir, string[] nonFatalFiles, bool includeSubDir)
		{
			List<ScriptItem> scriptItems = ScriptItem.GetScriptItemsFromDir(dir, nonFatalFiles, includeSubDir);
			this.Scripts.AddRange(scriptItems);
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

		internal Dictionary<string, object> RData = new Dictionary<string, object>();
		internal string GetRData()
		{
			this.RData.Add("ff_appversion", FormulaConfig.AppVersion);
			this.RData.Add("ff_scripts", this.Scripts);

			if (this.IsFirstPageLoad == true)
			{
				this.RData.Add("ff_polyfills", Utility.Polyfill.GetPolyfills(this));
				this.RData.Add("ff_cdn", FormulaConfig.Cdn);
			}
			else // SPA load
			{
				this.RData.Add("ff_title", this.Title);
				this.RData.Add("ff_webobjectstyles", this.WebObjectStyles);
			}

			string x = JsonConvert.SerializeObject(RData, EngineItems.JsonSettings);
			return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(x));
		}
		public void AddRData(string key, object value)
		{
			this.RData.Add(key, value);
		}
	}

	internal enum FfFolderType
	{
		App, Layout, Page
	}

	public class ScriptItem
	{
		public string Path;
		public bool IsFatal;

		internal static ConcurrentDictionary<string, List<ScriptItem>> CachedScriptItems = new ConcurrentDictionary<string, List<ScriptItem>>();
		internal static List<ScriptItem> GetScriptItemsFromDir(string dir, string[] nonFatalFiles, bool includeSubDir)
		{
			dir = dir.ToLowerInvariant();

			if(FormulaConfig.CacheJsAndCss == true && CachedScriptItems.ContainsKey(dir))
				return CachedScriptItems[dir];
			List<ScriptItem> scriptItems = new List<ScriptItem>();

			string dir2 = System.IO.Path.Combine("wwwroot", dir.Trim('/', '\\'));
			var jsFiles = FileHelper.GetFilesInDirectory(dir2, "js", includeSubDir);

			foreach (string jsFile in jsFiles)
			{
				var scriptItem = new ScriptItem();
				string lastWriteTime = File.GetLastWriteTimeUtc(jsFile).Ticks.ToString();
				var fileVersion = System.Convert.ToBase64String(Encoding.ASCII.GetBytes(lastWriteTime));

				string jsFilePath = string.Join('/', jsFile.Substring(8).Split('\\', StringSplitOptions.RemoveEmptyEntries));
				jsFilePath = $"/{jsFilePath}?v={fileVersion}".ToLowerInvariant();
				scriptItem.Path = jsFilePath;
				var fileNameWithoutExt = Helpers.MiscHelper.GetFileNameWithoutExt(jsFile);
				scriptItem.IsFatal = nonFatalFiles == null || nonFatalFiles.Contains(fileNameWithoutExt) == false;
				scriptItems.Add(scriptItem);
			}

			if (FormulaConfig.CacheJsAndCss)
				CachedScriptItems.TryAdd(dir, scriptItems);
			return scriptItems;
		}

	}
}
