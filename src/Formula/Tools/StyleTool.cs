using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Formula.Helpers;
using Microsoft.Extensions.FileProviders;

namespace Formula.Tools
{
    public static class StyleTool
    {
		static readonly ConcurrentDictionary<string, List<string>> _cachedStyleStringsExcludeSubDir = new ConcurrentDictionary<string, List<string>>();
		static readonly ConcurrentDictionary<string, List<string>> _cachedStyleStringsIncludeSubDir = new ConcurrentDictionary<string, List<string>>();
		static readonly string _wwwrootDirectory = $"{Path.DirectorySeparatorChar}wwwroot";
		internal static List<string> GetStyleElementStrings(string dirWithoutWwwroot, bool includeSubDir, string webobjectName = null)
		{
			string systemDirWithoutWwwroot = PathHelper.ToSystemPath(dirWithoutWwwroot);
			var targetStyleDic = includeSubDir ? _cachedStyleStringsIncludeSubDir : _cachedStyleStringsExcludeSubDir;

			List<string> styleElementStrings = new List<string>();
			string systemDirWithWwwroot = $"{_wwwrootDirectory}{systemDirWithoutWwwroot}";
			var cssSystemPathsWithWwwroot = FileHelper.GetFilesInDirectory(systemDirWithWwwroot, "css", includeSubDir);

			foreach (string cssSystemPathWithWwwroot in cssSystemPathsWithWwwroot)
			{
				string htmlStyleValue = GetHtmlStyleValue(cssSystemPathWithWwwroot, webobjectName);
				if (string.IsNullOrWhiteSpace(htmlStyleValue) == false)
					styleElementStrings.Add(htmlStyleValue);
			}
			return styleElementStrings;
		}

		static string GetHtmlStyleValue(string systemPathWithWwwroot, string webobjectName = null)
		{
			string css = GetStyleStringFromFile(systemPathWithWwwroot);
			string drawedStyle = null;
			string urlPath = PathHelper.ToUrlPath(systemPathWithWwwroot.Substring(_wwwrootDirectory.Length));
			if (string.IsNullOrWhiteSpace(css) == false)
			{
				drawedStyle = string.IsNullOrWhiteSpace(webobjectName) ?
					$"<style ff-path='{urlPath}'>{ css }</style>" :
					$"<style ff-path='{urlPath}' ff-webobject-name='{webobjectName}'>{ css }</style>";
			}
			return drawedStyle;
		}

		static string GetStyleStringFromFile(string systemPathWithWwwroot)
		{
			IFileInfo cssFile = FileHelper.GetFileInfo(systemPathWithWwwroot);
			if (cssFile == null)
				return null;

			using var stream = cssFile.CreateReadStream();
			using var streamReader = new StreamReader(stream);
			return streamReader.ReadToEnd();
		}
	}
}
