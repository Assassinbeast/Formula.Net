using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formula
{
    public class AppData
    {
        public int AppVersion { get; set; }    
        public string PageVersionApiUrl { get; set; }

		public AppData(Int32 appVersion, String pageVersionApiUrl)
		{
			this.AppVersion = appVersion;
			this.PageVersionApiUrl = pageVersionApiUrl;
		}
	}
}
