using System;

namespace Formula
{
    public abstract class LogicAction
    {
		public static LogicActions.Continue Continue()
		{
			return new LogicActions.Continue();
		}
		public static LogicActions.RedirectDraw RedirectDraw(Type pageType)
		{
			return new LogicActions.RedirectDraw(pageType);
		}
	}

	namespace LogicActions
	{
		public class Continue : LogicAction { }
		public class RedirectDraw: LogicAction
		{
			public Type PageType;
			public RedirectDraw(Type pageType)
			{
				this.PageType = pageType;
			}
		}
	}

	public enum RedirectStatus
	{
		MovedPermanently = 301,
		/// <summary>
		/// This should be used when you for example want to see your account, but not logged in, then
		/// redirect from /account to /login
		/// </summary>
		Found = 302,
		TemporaryRedirect = 307
	}
}
