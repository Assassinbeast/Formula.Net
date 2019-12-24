using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Formula.Processing
{
	internal static class DrawResultServices
	{
		public static Type DefaultLayout => FormulaConfig.DefaultLayoutType;
		public static Dictionary<Type, DrawResult> DrawResults = new Dictionary<Type, DrawResult>();
		public static string RootPageNamespace;
		private static Assembly _assembly;

		public static void Initialize(Assembly assembly, Type rootPageType)
		{
			_assembly = assembly;
			RootPageNamespace = string.Join('.', rootPageType.Namespace.Split('.').SkipLast(1));

			var pageTypes = GetPageTypes();
			foreach (Type pageType in pageTypes)
			{
				List<Type> tree = GetParentPagesIncludeSelf(pageType);
				//Console.WriteLine(JsonConvert.SerializeObject(tree.Select(x=>x.FullName), Formatting.Indented));

				Type first = tree.First();
				tree.Reverse();
				Type layoutType = DefaultLayout;
				Type highestParent = tree.First();
				var meta = highestParent.GetCustomAttribute<MetaDataAttribute>();
				layoutType = meta?.Layout != null ? meta.Layout : layoutType;
				DrawResults.Add(first, new DrawResult(layoutType, tree));
			}

			HashSet<Type> removingPageTypes = new HashSet<Type>();
			foreach (var item in DrawResults)
				foreach (var removingPageType in item.Value.PageTypes.SkipLast(1))
					removingPageTypes.Add(removingPageType);
			foreach (var removingPageType in removingPageTypes)
				DrawResults.Remove(removingPageType);


			//Console.WriteLine(JsonConvert.SerializeObject(
			//	DrawResults.Select(x =>
			//	new
			//	{
			//		Layout = x.Value.LayoutType.FullName,
			//		Key = x.Key.FullName,
			//		Value = x.Value.PageTypes.Select(x => x.FullName)
			//	}),
			//	Formatting.Indented));

		}
		public static List<Type> GetParentPagesIncludeSelf(Type pageType)
		{
			List<Type> pageTypes = new List<Type>();

			Type curPageType = pageType; //Contoso.Pages.Account.Summary.SummaryPage
			while (true)
			{
				if (curPageType.Namespace.StartsWith(RootPageNamespace) == false)
					throw new ArgumentException($"The PageType '{curPageType}' must be within the namespace '{RootPageNamespace}'");

				pageTypes.Add(curPageType);
				string parentFullTypeName = GetParentPageFullName(curPageType);
				if (parentFullTypeName == null)
					break;

				Type parentPageType = _assembly.GetType(parentFullTypeName);
				if (parentPageType == null)
					throw new ArgumentException($"The page '{parentFullTypeName}' must exist, because a child page exist '{curPageType.FullName}'");
				curPageType = parentPageType;
			}

			return pageTypes;
		}

		static string GetParentPageFullName(Type pageType)
		{
			//pageType is eg: Contoso.Pages.Account.Summary.SummaryPage
			string parentNamespace = string.Join('.', pageType.Namespace.Split('.').SkipLast(1)); //Contoso.Pages.Account
			if (parentNamespace == RootPageNamespace || parentNamespace.StartsWith(RootPageNamespace) == false)
				return null;

			string parentPageName = $"{parentNamespace.Split('.')[^1]}Page"; //AccountPage
			string parentFullTypeName = $"{parentNamespace}.{parentPageName}"; //Contoso.Pages.Account.AccountPage
			return parentFullTypeName;
		}

		static Type[] GetPageTypes()
		{
			return (from type in _assembly.GetTypes()
					where type.IsSubclassOf(typeof(BasePage)) && !type.IsAbstract
					select type).ToArray();
		}

		public static DrawResult GetDrawResult(Type pageType)
		{
			return DrawResults[pageType];
		}
	}
}
