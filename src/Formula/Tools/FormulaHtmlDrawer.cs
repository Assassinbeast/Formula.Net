using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Formula.Tools
{
	internal static class FormulaHtmlDrawer
	{
		public static async Task<HtmlDocument> DrawFormulaHtml(BaseApp app, BaseLayout layout, List<BasePage> pages, IViewRender render)
		{
			List<(Type type, string html)> draws = new List<(Type type, string html)>();
			if (app.ShallDraw)
				draws.Add((app.GetType(), ProcessApp(app, await render.RenderAsync("app/app", app))));
			if (layout.ShallDraw)
				draws.Add((layout.GetType(), ProcessLayout(layout.GetType(), await render.RenderAsync(PathHelper.View(layout.GetType()), layout))));
			foreach (var page in pages)
			{ 
				if (page.ShallDraw)
					draws.Add((page.GetType(), ProcessPage(page.GetType(), await render.RenderAsync(PathHelper.View(page.GetType()), page))));
			}

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
			string appDir = PathHelper.GetAppDir();

			styleStrings = StyleTool.GetStyleElementStrings(appDir, true);
			foreach (string styleString in styleStrings)
				ffApp.PrependChild(HtmlNode.CreateNode(styleString));

			return doc.DocumentNode.OuterHtml;
		}
		static string ProcessLayout(Type layoutType, string layoutHtml)
		{
			string layoutDir = PathHelper.GetLayoutDir(layoutType); //TODO: styles are cachable

			List<string> styleStrings = StyleTool.GetStyleElementStrings(layoutDir, true);
			StringBuilder sb = new StringBuilder();
			foreach (string styleString in styleStrings)
				sb.Append(styleString);

			return $"<ff-layout ff-name='{PathHelper.LayoutName(layoutType)}'>{sb.ToString()}{layoutHtml}</ff-layout>";
		}
		static string ProcessPage(Type pageType, string layoutHtml)
		{
			string pageDir = PathHelper.GetPageDir(pageType); //TODO: styles are cachable
			List<string> styleStrings = StyleTool.GetStyleElementStrings(pageDir, false);
			StringBuilder sb = new StringBuilder();
			foreach (string style in styleStrings)
				sb.Append(style);

			return $"<ff-page ff-name='{PathHelper.PageName(pageType)}'>{sb.ToString()}{layoutHtml}</ff-page>";
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
		public static string DrawRData(FormulaContext formulaContext)
		{
			return $"<div id='ff-rdata' data-x='{formulaContext.GetRData()}'></div>";
		}
	}
}
