﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace Formula.Tools
{
	internal static class FormulaHtmlDrawer
	{
		public static async Task<HtmlDocument> DrawFormulaHtml(BaseApp app, BaseLayout layout, List<BasePage> pages, IViewRender render)
		{
			List<(Type type, string html)> draws = new List<(Type type, string html)>();
			draws.Add((app.GetType(), ProcessApp(app, await render.RenderAsync("app/app", app))));
			draws.Add((layout.GetType(), ProcessLayout(layout, await render.RenderAsync(FormulaPathHelper.View(layout.GetType()), layout))));
			foreach (var page in pages)
				draws.Add((page.GetType(), ProcessPage(page, await render.RenderAsync(FormulaPathHelper.View(page.GetType()), page))));

			var doc = new HtmlDocument();
			doc.LoadHtml(draws[0].html);

			if (draws.Count == 1)
				return doc;

			HtmlNode ffFolder = doc.DocumentNode.Descendants("ff-folder").FirstOrDefault();
			if (ffFolder == null)
				throw new Exception($"The ViewController {draws[0].type.FullName}'s cshtml file must have a <ff-folder> element because its not a BasePageEnd");
			for (int i = 1; i < draws.Count; i++)
			{
				HtmlNode nextDrawNode = HtmlNode.CreateNode(draws[i].html);
				ffFolder.AppendChild(nextDrawNode);
				if (i + 1 < draws.Count)
				{
					ffFolder = nextDrawNode.Descendants("ff-folder").FirstOrDefault();
					if (ffFolder == null)
						throw new Exception($"The ViewController '{draws[i].type.FullName}'s ' cshtml file must have a <ff-folder> element because its not a BasePageEnd");
				}
			}

			return doc;
		}

		static string ProcessApp(BaseApp app, string appHtml)
		{
			var doc = new HtmlDocument(); //TODO: styles are cachable
			doc.LoadHtml(appHtml);

			HtmlNode head = doc.DocumentNode.SelectSingleNode("//head");

			List<string> styleStrings = StyleTool.GetStyleElementStrings("formula", false);
			foreach (string styleString in styleStrings)
				head.AppendChild(HtmlNode.CreateNode(styleString));

			HtmlNode ffApp = doc.DocumentNode.SelectSingleNode("//ff-app");
			string appDir = FormulaPathHelper.GetAppDir();

			styleStrings = StyleTool.GetStyleElementStrings(appDir, true);
			foreach (string styleString in styleStrings)
				ffApp.PrependChild(HtmlNode.CreateNode(styleString));

			return doc.DocumentNode.OuterHtml;
		}
		static string ProcessLayout(BaseLayout layout, string layoutHtml)
		{
			string layoutDir = FormulaPathHelper.GetLayoutDir(layout.GetType());

			List<string> styleStrings = StyleTool.GetStyleElementStrings(layoutDir, true);
			StringBuilder sb = new StringBuilder();
			foreach (string styleString in styleStrings)
				sb.Append(styleString);

			string variantJson = JsonConvert.SerializeObject(layout.Variant);
			string variant = layout.Variant != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(variantJson)) : "";
			return $"<ff-layout ff-name='{FormulaPathHelper.LayoutName(layout.GetType())}' ff-variant='{variant}'>{sb.ToString()}{layoutHtml}</ff-layout>";
		}
		static string ProcessPage(BasePage page, string layoutHtml)
		{
			string pageDir = FormulaPathHelper.GetPageDir(page.GetType());
			List<string> styleStrings = StyleTool.GetStyleElementStrings(pageDir, false);
			StringBuilder sb = new StringBuilder();
			foreach (string style in styleStrings)
				sb.Append(style);

			string variantJson = JsonConvert.SerializeObject(page.Variant);
			string variant = page.Variant != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(variantJson)) : "";
			return $"<ff-page ff-name='{FormulaPathHelper.PageName(page.GetType())}' ff-variant='{variant}'>{sb}{layoutHtml}</ff-page>";
		}
		public static void InsertWebObjectStyles(HtmlNode webobjectStyleDiv, HashSet<string> webObjectNames)
		{
			foreach (string webobjectName in webObjectNames)
			{
				string dir = "webobjects/" + string.Join('/', webobjectName.Split('.'));
				List<string> webobjectStyles = StyleTool.GetStyleElementStrings(dir, false, webobjectName);
				foreach (string style in webobjectStyles)
					webobjectStyleDiv.AppendChild(HtmlNode.CreateNode(style));
			}
		}
		public static void InsertPreloadModuleScripts(HtmlNode head, IEnumerable<string> paths)
		{
			foreach (string path in paths)
				head.AppendChild(HtmlNode.CreateNode($"<link rel='modulepreload' href='{path}'>"));
		}
	}
}
