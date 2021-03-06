﻿using System;

namespace Formula
{
	public class MetaDataAttribute : Attribute
	{
		public Type Layout;
	}
	public class HeaderAttribute : Attribute
	{
		public string Key;
		public HeaderAttribute() { }
		public HeaderAttribute(string key)
		{
			this.Key = key;
		}
	}
	public class CookieAttribute : Attribute
	{
		public string Key;
		public CookieAttribute() { }
		public CookieAttribute(string key)
		{
			this.Key = key;
		}
	}
	public class QueryAttribute : Attribute
	{
		public string Key;
		public QueryAttribute() { }
		public QueryAttribute(string key)
		{
			this.Key = key;
		}
	}
	public class NonFatalJsAttribute: Attribute
	{
		public string[] JsFiles;

		/// <param name="jsFiles">Put in the filepaths without</param>
		public NonFatalJsAttribute(params string[] jsFiles)
		{
			this.JsFiles = jsFiles;
		}
	}
}
