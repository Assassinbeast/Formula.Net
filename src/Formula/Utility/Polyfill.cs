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

        static readonly string _wwwrootDir = $"{Path.DirectorySeparatorChar}wwwroot";
        static List<PolyfillItem> GetPolyfillItems(string folderName)
        {
            var rootPolyfillSystemDirWithWwwroot = $"{Path.DirectorySeparatorChar}{Path.Combine("wwwroot", "formula", "polyfills", folderName)}";
            var polyfillItemSystemDirsWithWwwroot = FileHelper.GetDirectoriesInDirectory(rootPolyfillSystemDirWithWwwroot);
            var polyfills = new List<PolyfillItem>();
            var polyfillItemCorrectDirNames = GetCorrectCasingDirNamesInDirectory_LowerCase_CorrectCase_Dic(
                $"{Path.DirectorySeparatorChar}{Path.Combine("formula", "polyfills", folderName)}");
            foreach (string polyfillItemSystemDirWithWwwroot in polyfillItemSystemDirsWithWwwroot)
            {
                string polyfillItemSystemDirWithoutWwwroot = polyfillItemSystemDirWithWwwroot.Substring(_wwwrootDir.Length);
                var polyfillItemDirName = FileHelper.FileProvider.GetFileInfo(polyfillItemSystemDirWithoutWwwroot).Name.ToLowerInvariant();
                var systemFilesWithWwwroot = FileHelper.GetFilesInDirectory(polyfillItemSystemDirWithWwwroot, "js", true);
                var urlFilesWithoutWwwroot = systemFilesWithWwwroot.Select(
                    x=> PathHelper.ToUrlPath(x.Substring(_wwwrootDir.Length))).ToList();
                if (polyfillItemCorrectDirNames.ContainsKey(polyfillItemDirName))
                    polyfills.Add(new PolyfillItem(polyfillItemCorrectDirNames[polyfillItemDirName], urlFilesWithoutWwwroot));
            }
            return polyfills;
        }

        static Dictionary<string, string> GetCorrectCasingDirNamesInDirectory_LowerCase_CorrectCase_Dic(string systemDir)
        {
            var systemChildDirs = FileHelper.GetDirectoriesInDirectory(systemDir);
            var lowerCaseCorrectCaseDic = new Dictionary<string, string>();
            foreach (var systemChildDir in systemChildDirs)
            {
                var dirName = FileHelper.FileProvider.GetFileInfo(systemChildDir).Name;
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
