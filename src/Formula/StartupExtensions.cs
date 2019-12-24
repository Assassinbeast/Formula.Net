using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Formula.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Formula
{
	public static class StartupExtensions
	{
		public static void UseFormula(this IApplicationBuilder app, IWebHostEnvironment env, FormulaConfigArg settings)
		{
			FormulaController.InitializeApp(env, settings);
			app.Run(FormulaController.Run);
		}

		/// <summary>
		/// Remember to use services.AddMvc(). Formula depends on it.
		/// </summary>
		public static void AddFormula<TPageTypeSelector>(this IServiceCollection services, IWebHostEnvironment env)
			where TPageTypeSelector : class, IPageTypeSelector
		{
			services.AddScoped<IPageTypeSelector, TPageTypeSelector>();
			services.AddScoped<IViewRender, ViewRender>();
			Helpers.FileHelper.FileProvider = env.ContentRootFileProvider;

			IEnumerable<Type> viewCtrls = Assembly.GetEntryAssembly().GetTypes()
				.Where(x => x.IsSubclassOf(typeof(ViewController)) && x.IsAbstract == false);
			foreach (Type viewCtrl in viewCtrls)
				services.AddTransient(viewCtrl);
		}
	}
}
