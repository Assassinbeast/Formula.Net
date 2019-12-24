using System;
using System.Collections.Generic;

namespace Formula.Processing
{
	internal class DrawResult
	{
		public Type LayoutType;
		public List<Type> PageTypes;

		public DrawResult(Type layoutType, List<Type> pageTypes)
		{
			this.LayoutType = layoutType;
			this.PageTypes = pageTypes;
		}
	}
}
