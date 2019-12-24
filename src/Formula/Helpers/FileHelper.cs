using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileProviders;

namespace Formula.Helpers
{
    public static class FileHelper
    {
		public static IFileProvider FileProvider;

		/// <summary>
		/// Returns null if not exist
		/// </summary>
		public static IFileInfo GetFileInfo(string filePath)
		{
			string dirPath = Path.GetDirectoryName(filePath);
			return FileProvider.GetDirectoryContents(dirPath)
				.FirstOrDefault(
					x => x.Exists && 
					!x.IsDirectory && 
					 x.Name == Path.GetFileName(filePath));
		}

		public static List<string> GetFilesInDirectory(string directory, string extension, bool includeSubDir)
		{
			List<string> files = new List<string>();
			files.AddRange(_GetFilesInDirectory(directory, extension));
			if (includeSubDir != true) 
				return files;

			var subDirs = GetDirectoriesInDirectory(directory);
			foreach (string subDir in subDirs)
				files.AddRange(GetFilesInDirectory(subDir, extension, includeSubDir));
			return files;
		}

		private static IEnumerable<string> _GetFilesInDirectory(string directory, string extension)
		{
			//directory can be eg wwwroot/hello
			var files = FileProvider.GetDirectoryContents(directory)
				.Where(x => x.Exists && !x.IsDirectory && 
				(string.IsNullOrWhiteSpace(extension) || x.Name.EndsWith("." + extension)))
				.Select(x => Path.Combine(directory, x.Name)); 

			return files; //["wwwroot/hello/foo.js", "wwwroot/hello/bar.js"]
		}

		public static IEnumerable<string> GetDirectoriesInDirectory(string parentDir)
		{
			//directory must be eg wwwroot/hello
			var directories = FileProvider.GetDirectoryContents(parentDir)
				.Where(x => x.Exists && x.IsDirectory)
				.Select(x => Path.Combine(parentDir, x.Name));

			return directories; //["wwwroot/hello/menus", "wwwroot/hello/smileys"]
		}
	}
}
