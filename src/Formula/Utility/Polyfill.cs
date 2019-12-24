using System.Collections.Generic;
using System.IO;
using System.Linq;
using Formula.Helpers;

namespace Formula.Utility
{
    internal class Polyfill
    {
        static Polyfill _polyfill;
        public static Polyfill GetPolyfills(FormulaContext tc)
        {
            /* Returns eg
            {
                "modernizr": [
                    {
                        "folderName": "fetch",
                        "jsFiles": [
                            "/formula/polyfills/modenizr/fetch/es6-promise.auto.js",
                            "/formula/polyfills/modenizr/fetch/fetch.js"
                        ]
                    }
                ],
                "evaluation": [
                    {
                        "folderName": "Element.prototype.append",
                        "jsFiles": [
                            "/formula/polyfills/eval/element.prototype.append/elementappendpolyfill.js"
                        ]
                    }
                ]
            }
            */
            return _polyfill ??= new Polyfill
	        {
		        Evaluation = GetPolyfillItems("evaluation"),
		        Modernizr = GetPolyfillItems("modenizr")
	        };
        }

        static List<PolyfillItem> GetPolyfillItems(string folderName)
        {
            var rootPolyfillFolder = Path.Combine("wwwroot", "formula", "polyfills", folderName);
            var polyfillItemDirsFullPath = FileHelper.GetDirectoriesInDirectory(rootPolyfillFolder);
            var polyfills = new List<PolyfillItem>();
            var polyfillItemCorrectDirNames = GetCorrectCasingDirNamesInDirectory_LowerCase_CorrectCase_Dic(
                Path.Combine("formula", "polyfills", folderName));
            foreach (string dir in polyfillItemDirsFullPath)
            {
                string sourceFolder = Path.Combine(dir.Split('/', '\\').Skip(1).ToArray());
                var dirName = FileHelper.FileProvider.GetFileInfo(sourceFolder).Name;
                var files = FileHelper.GetFilesInDirectory(dir, "js", true);
                for (int i = 0; i < files.Count; i++)
                    files[i] = $"/{string.Join('/', files[i].Split('/', '\\').Skip(1)).TrimStart('/')}";
                if (polyfillItemCorrectDirNames.ContainsKey(dirName))
                    polyfills.Add(new PolyfillItem(polyfillItemCorrectDirNames[dirName], files));
            }
            return polyfills;
        }

        static Dictionary<string, string> GetCorrectCasingDirNamesInDirectory_LowerCase_CorrectCase_Dic(string dirPath)
        {
            var dirs = FileHelper.GetDirectoriesInDirectory(dirPath);
            var lowerCaseCorrectCaseDic = new Dictionary<string, string>();
            foreach (var dir in dirs)
            {
                var dirName = FileHelper.FileProvider.GetFileInfo(dir).Name;
                lowerCaseCorrectCaseDic.Add(dirName.ToLowerInvariant(), dirName);
            }
            return lowerCaseCorrectCaseDic;
        }

        public List<PolyfillItem> Modernizr;
        public List<PolyfillItem> Evaluation;
        public class PolyfillItem
        {
            public string FolderName;
            public List<string> JsFiles;

            public PolyfillItem(string folderName, List<string> jsFiles)
            {
                this.FolderName = folderName;
                this.JsFiles = jsFiles;
            }
        }
    }
}
