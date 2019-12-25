using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Formula.Utility
{
    public static class FatalJsFileCaches
    {
        static readonly ConcurrentDictionary<Type, string[]> _viewControllerNonFatalJsFilesDic = new ConcurrentDictionary<Type, string[]>();
        public static string[] GetNonFatalJsFiles(Type type)
        {
            if (FormulaConfig.CacheJsAndCss && _viewControllerNonFatalJsFilesDic.ContainsKey(type))
                return _viewControllerNonFatalJsFilesDic[type];

            var jsFiles = type.GetCustomAttribute<NonFatalJsAttribute>()?.JsFiles;
            for (int i = 0; i < jsFiles.Length; i++)
	            jsFiles[i] = jsFiles[i].ToLowerInvariant();

            if (FormulaConfig.CacheJsAndCss)
                _viewControllerNonFatalJsFilesDic.TryAdd(type, jsFiles);
            return jsFiles;
        }
    }
}
