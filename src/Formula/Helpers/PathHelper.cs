using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formula.Helpers
{
    public static class PathHelper
    {
        /// <summary>
        /// Returns a string that starts with PathSeperator. Eg: "\"
        /// </summary>
        public static string ToSystemPath(string path)
        {
            path = path.ToLowerInvariant();
            path = Path.Combine(path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            path = $"{Path.DirectorySeparatorChar}{path}";
            return path;
        }
        public static string ToUrlPath(string path)
        {
            path = path.ToLowerInvariant();
            path = string.Join('/', path.Split(new[]{'/', '\\'}, StringSplitOptions.RemoveEmptyEntries));
            return $"/{path}";
        }
    }
}
