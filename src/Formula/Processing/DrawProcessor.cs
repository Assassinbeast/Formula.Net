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
			context.App = app;
			context.SetTypeRef(app);
			string appDir = FormulaPathHelper.GetAppDir();
			context.AddScriptsFromDir(appDir, true);
			#endregion

			#region Add Layout
			BaseLayout layoutViewCtrl = (BaseLayout)serviceProvider.GetService(drawResult.LayoutType);
			layoutViewCtrl.Context = context;
			context.SetTypeRef(layoutViewCtrl);
			context.Layout = layoutViewCtrl;

			string layoutDir = FormulaPathHelper.GetLayoutDir(layoutViewCtrl.GetType());
			context.AddScriptsFromDir(layoutDir, true);
			#endregion

			#region Add Pages
			for (int i = 0; i < drawResult.PageTypes.Count; i++)
			{
				Type pageType = drawResult.PageTypes[i];
				var pageViewCtrl = (BasePage)serviceProvider.GetService(pageType);
				pageViewCtrl.Context = context;

				string pageDir = FormulaPathHelper.GetPageDir(pageViewCtrl.GetType());
				context.AddScriptsFromDir(pageDir, false);
				//context.AddStylesFromDir(pageDir);

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
					context.AddScriptsFromDir(dir, false);
				}
			}
			FormulaHtmlDrawer.InsertWebObjectStyles(htmlDoc.GetElementbyId("ff-webobject-styles"), drawingWebObjects);
			#endregion

			//Add PageData content
			htmlDoc.GetElementbyId("ff-pagedata").Attributes["content"].Value = context.GetPageData();

			//Add Scripts for preloading modules
			FormulaHtmlDrawer.InsertPreloadModuleScripts(htmlDoc.DocumentNode.SelectSingleNode("//head"), 
				context.Scripts.Where(x => x.Value.IsModule && x.Value.Path != "/formula/formula.js")
				.Select(x => x.Value.Path));

			//Return the final html
			return htmlDoc.DocumentNode.OuterHtml;

			#endregion
		}

		static async Task<string> ProcessNonContinueLogicAction(LogicAction logicAction, FormulaContext context)
		{
			if (logicAction is LogicActions.RedirectDraw)
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
