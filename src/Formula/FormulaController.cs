using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Formula.Helpers;
using Formula.Processing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Formula
{
	internal class FormulaController
	{
		public static async Task Run(HttpContext context)
		{
			LogRequest(context);
			context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
			HttpHelper.SetNoCacheOnResponseHeaders(context.Response.Headers);

			string html = await GetHtmlByRequest(context);

			if (string.IsNullOrWhiteSpace(html))
				return;
			await context.Response.WriteAsync(html);
		}

		static async Task<string> GetHtmlByRequest(HttpContext httpContext)
		{
			var path = HttpUtility.UrlDecode(httpContext.Request.Path);
			FormulaContext context = CreateFormulaContext(httpContext, path);
			if (context == null)
				throw new Exception($"{context.GetType().FullName} couldn't be created");

			var pageTypeSelector = (IPageTypeSelector)httpContext.RequestServices.GetService(typeof(IPageTypeSelector));
			var pageType = pageTypeSelector.Find(httpContext);
			DrawResult drawResult = DrawResultServices.GetDrawResult(pageType);
			return await DrawProcessor.Process(drawResult, context);
		}
		public static FormulaContext CreateFormulaContext(HttpContext context, string path)
		{
			FormulaContext formulaContext = new FormulaContext();
			formulaContext.Initialize(context, path);
			return formulaContext;
		}
		static void LogRequest(HttpContext context)
		{
			var logger = (ILogger<FormulaController>)context.RequestServices.GetService(typeof(ILogger<FormulaController>));

			IHeaderDictionary headers = context.Request.Headers;
			var requestLogJson = new
			{
				Path = context.Request.Path,
			};

			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"Request:");
			sb.AppendLine($"Path: {context.Request.Path}");

			string x = sb.ToString();
			logger.LogDebug(x);
		}
		public static void InitializeApp(IWebHostEnvironment env, FormulaConfigArg settings)
		{
			FormulaConfig.Initialize(env, settings);
			DrawResultServices.Initialize(settings.AppAssembly, settings.DefaultNamespace);
		}
	}
}
