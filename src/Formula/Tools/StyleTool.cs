using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Formula.Helpers;
using Microsoft.Extensions.FileProviders;

namespace Formula.Tools
{
    public static class StyleTool
    {
		static ConcurrentDictionary<string, List<string>> CachedStyleStrings =
			new ConcurrentDictionary<string, List<string>>();
		internal static List<string> GetStyleElementStrings(string dir, bool includeSubDir, string webobjectName = null)
		{
			dir = dir.ToLowerInvariant();

			if (FormulaConfig.CacheJsAndCss == true && CachedStyleStrings.ContainsKey(dir))
				return CachedStyleStrings[dir];

			List<string> styleElementStrings = new List<string>();
			string dir2 = Path.Combine("wwwroot", dir.Trim('/', '\\'));

			var cssFiles = FileHelper.GetFilesInDirectory(dir2, "css", includeSubDir);

			foreach (string cssFile in cssFiles)
			{
				//substring 8 to delete the "wwwroot/"
				string filePath = cssFile.Substring(8);
				string styleString = DrawStyle(filePath, webobjectName);
				if (string.IsNullOrWhiteSpace(styleString) == false)
					styleElementStrings.Add(styleString);
			}

			if (FormulaConfig.CacheJsAndCss)
				CachedStyleStrings.TryAdd(dir, styleElementStrings);
			return styleElementStrings;
		}

		static ConcurrentDictionary<string, string> cachedDrawedStyles = new ConcurrentDictionary<string, string>();
		static string GetStyleStringFromFile(string path)
		{
			string backendFilePath = Path.Combine("wwwroot", path.Trim('/', '\\'));
			
			IFileInfo cssFile = FileHelper.GetFileInfo(backendFilePath);
			if (cssFile == null)
				return null;

			using(var stream = cssFile.CreateReadStream())
			using (var streamReader = new StreamReader(stream))
			{
				return streamReader.ReadToEnd();
			}
		}
		static string DrawStyle(string path, string webobjectName = null)
		{
			if (FormulaConfig.CacheJsAndCss == true && cachedDrawedStyles.ContainsKey(path))
				return cachedDrawedStyles[path];

			string css = GetStyleStringFromFile(path);
			string drawedStyle = null;
			if(string.IsNullOrWhiteSpace(css) == false)
			{
				if (string.IsNullOrWhiteSpace(webobjectName) == true)
					drawedStyle = $"<style data-path='{path}'>{ css }</style>";
				else
					drawedStyle = $"<style data-path='{path}' data-webobject-name='{webobjectName}'>{ css }</style>";
			}

			if (FormulaConfig.CacheJsAndCss)
				cachedDrawedStyles.TryAdd(path, drawedStyle);
			return drawedStyle;
		}
	}
}
