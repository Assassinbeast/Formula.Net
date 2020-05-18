using Formula.Helpers;
using Formula.LogicActions;
using Formula.Tools;
using Formula.Utility;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Formula.Processing
{
	internal static class DrawProcessor
	{
		public static async Task<string> Process(DrawResult drawResult,
			FormulaContext context)
		{
			IServiceProvider serviceProvider = context.HttpContext.RequestServices;

			#region Add App
			var app = (BaseApp)serviceProvider.GetService(FormulaConfig.AppType);
			app.Context = context;
			app.ShallDraw = DrawApp(context);
			context.App = app;
			context.SetTypeRef(app);
			if (app.ShallDraw)
			{
				string appDir = FormulaPathHelper.GetAppDir();
				context.AddScriptsFromDir(appDir, true);
			}
			#endregion

			#region Add Layout
			BaseLayout layoutViewCtrl = (BaseLayout)serviceProvider.GetService(drawResult.LayoutType);
			layoutViewCtrl.Context = context;
			layoutViewCtrl.ShallDraw = DrawLayout(context, drawResult.LayoutType);
			context.SetTypeRef(layoutViewCtrl);
			context.Layout = layoutViewCtrl;
			if (layoutViewCtrl.ShallDraw)
			{
				string layoutDir = FormulaPathHelper.GetLayoutDir(layoutViewCtrl.GetType());
				context.AddScriptsFromDir(layoutDir, true);
			}
			#endregion

			#region Add Pages
			bool hasLayoutOrPageBeenDrawn = layoutViewCtrl.ShallDraw;
			for (int i = 0; i < drawResult.PageTypes.Count; i++)
			{
				bool isLastPageToDraw = drawResult.PageTypes.Count == i + 1;
				int lastPageDrawnIndex = i - 1;
				Type pageType = drawResult.PageTypes[i];

				bool shallDrawPage = DrawPage(
					context,
					hasLayoutOrPageBeenDrawn,
					pageType,
					lastPageDrawnIndex == -1 ? drawResult.LayoutType : drawResult.PageTypes[i - 1],
					isLastPageToDraw);
				hasLayoutOrPageBeenDrawn = shallDrawPage;

				var pageViewCtrl = (BasePage)serviceProvider.GetService(pageType);
				pageViewCtrl.Context = context;
				pageViewCtrl.ShallDraw = shallDrawPage;

				if (pageViewCtrl.ShallDraw)
				{
					string pageDir = FormulaPathHelper.GetPageDir(pageViewCtrl.GetType());
					context.AddScriptsFromDir(pageDir, false);
					//context.AddStylesFromDir(pageDir);
				}

				context.Pages.Add(pageViewCtrl);
				context.SetTypeRef(pageViewCtrl);
			}
			#endregion

			#region Process all ViewControllers ProcessLogic functions
			for (int i = -2; i < context.Pages.Count; i++)
			{
				ViewController viewCtrl;

				#region Set viewCtrl, shallDraw and add page scripts (if its a page)
				if (i == -2) //If App
					viewCtrl = app;
				else if (i == -1) //If Layout
					viewCtrl = layoutViewCtrl;
				else //Its Page
					viewCtrl = context.Pages[i];
				#endregion

				DrawProcessorHelper.SetViewControllerParameters(context, viewCtrl);

				LogicAction logicAction;
				if (DrawProcessorHelper.ShallFireAsyncProcessLogic(viewCtrl))
					logicAction = await viewCtrl.ProcessLogicAsync();
				else
					logicAction = viewCtrl.ProcessLogic();

				if (logicAction is LogicActions.Continue == false)
					return await ProcessNonContinueLogicAction(logicAction, context);
			}
			#endregion

			#region Draw / Create the html string
			HtmlDocument htmlDoc;

			#region Draw App, Layout & Pages
			List<Type> drawingPages = new List<Type>();
			foreach (BasePage page in context.Pages)
				if (page.ShallDraw)
					drawingPages.Add(page.GetType());
			htmlDoc = await FormulaHtmlDrawer.DrawFormulaHtml(
				app, layoutViewCtrl, context.Pages,
				(IViewRender)context.HttpContext.RequestServices.GetService(typeof(IViewRender)));
			#endregion

			HashSet<string> drawingWebObjects = new HashSet<string>(); //WebObjects names in the html

			#region Add WebObject styles & scripts to context.Styles and Scripts
			HtmlNodeCollection webObjectNodes = htmlDoc.DocumentNode.SelectNodes("//*[@ff-webobject]");
			if (webObjectNodes != null)
			{
				foreach (HtmlNode webobjNode in webObjectNodes)
				{
					HtmlAttribute attr = webobjNode.Attributes["ff-webobject"];
					drawingWebObjects.Add(attr.Value);
				}
				foreach (string drawingWebObject in drawingWebObjects)
				{
					//Eg: "menus.TopMenu"

					string dir = "webobjects/" + string.Join('/', drawingWebObject.Split('.'));
					Type webobjectType = FormulaPathHelper.GetTypeByWebObjectName(drawingWebObject);
					context.AddScriptsFromDir(dir, false);
					if (context.IsFirstPageLoad == false && context.DrawnWebObjects.Contains(drawingWebObject) == false)
						context.AddWebObjectStylesFromDir(dir, drawingWebObject);
				}
			}
			#endregion

			#region Finish the html text
			if (context.IsFirstPageLoad == true)
			{
				FormulaHtmlDrawer.InsertWebObjectStyles(htmlDoc.GetElementbyId("ff-webobject-styles"), drawingWebObjects);

				var pageData = htmlDoc.GetElementbyId("ff-pagedata");
				pageData.Attributes["content"].Value = context.GetPageData();

				return htmlDoc.DocumentNode.OuterHtml;
			}
			else //SPA-Load
			{
				return htmlDoc.DocumentNode.OuterHtml;
			}
			#endregion

			#endregion
		}

		static bool DrawApp(FormulaContext context)
		{
			if (context.IsFirstPageLoad == true)
				return true;
			return false;
		}
		static bool DrawLayout(FormulaContext requestRequestContext, Type layoutType)
		{
			if (requestRequestContext.IsFirstPageLoad == true)
				return true;
			else //Not FirstPageLoad
			{
				//Check if its the same layout that must be drawn
				if (requestRequestContext.IsClientLayoutDrawn(layoutType) == true)
					return false; //Return false to indicate that the layout shouldn't be drawn
				else //Not drawn, so draw it
				{
					requestRequestContext.SetTargetFfFolder(FfFolderType.App, null);
					return true;
				}
			}
		}
		static bool DrawPage(FormulaContext context, bool hasLayoutOrAnyPageBeenDrawn, Type pageType, Type parentLayoutOrPage, bool forceDraw)
		{
			//forceDraw is true if its the last page
			if (context.IsClientPageDrawn(pageType) && forceDraw == false)
				return false;

			if (hasLayoutOrAnyPageBeenDrawn)
				return true;
			//This page is the first to be drawn
			//So now set an ffFolder
			if (FormulaPathHelper.IsTypeALayout(parentLayoutOrPage) == true)
				context.SetTargetFfFolder(FfFolderType.Layout, null);
			else
				context.SetTargetFfFolder(FfFolderType.Page, FormulaPathHelper.PageName(parentLayoutOrPage));
			return true;
		}

		static async Task<string> ProcessNonContinueLogicAction(LogicAction logicAction, FormulaContext context)
		{
			if (logicAction is UrlRedirect urlRedirect)
			{
				bool redirectToSameOrigin;
				if (urlRedirect.Location.StartsWith("http://") || urlRedirect.Location.StartsWith("https://"))
				{
					redirectToSameOrigin = urlRedirect.Location.StartsWith(context.HttpContext.Request.Host.Value);
					if (redirectToSameOrigin)
						urlRedirect.Location = urlRedirect.Location.Substring(context.HttpContext.Request.Host.Value.Length);
				}
				else
					redirectToSameOrigin = true;

				if (redirectToSameOrigin == false || context.IsFirstPageLoad)
				{
					context.HttpContext.Response.Headers["location"] = urlRedirect.Location;
					context.HttpContext.Response.StatusCode = urlRedirect.StatusCode;
				}
				else //Get here: Redirect to another path by internal handling in frontned
				{
					context.HttpContext.Response.Headers["ff_redirect"] = "true";
					context.HttpContext.Response.Headers["location"] = urlRedirect.Location;
					context.HttpContext.Response.StatusCode = 200;
				}
				return null;
			}
			else if (logicAction is LogicActions.RedirectDraw)
			{
				var redirectDraw = (LogicActions.RedirectDraw)logicAction;
				DrawResult drawResult = DrawResultServices.GetDrawResult(redirectDraw.PageType);
				return await Process(drawResult, FormulaController.CreateFormulaContext(
					context.HttpContext, context.Path));
			}
			throw new System.ArgumentException("Fatal error in the Formula framework");
		}
	}
}
