using System.Threading.Tasks;

namespace Formula
{
	public abstract class ViewController
	{
		public FormulaContext Context;

		public virtual LogicAction ProcessLogic()
		{
			return LogicAction.Continue();
		}
		public virtual Task<LogicAction> ProcessLogicAsync()
		{
			return Task.FromResult((LogicAction)LogicAction.Continue());
		}
	}

	public abstract class BaseApp : ViewController { }
	public abstract class BaseLayout : ViewController 
	{
		public string Variant { get; set; }
	}
	public abstract class BasePage : ViewController
	{
		public object Variant { get; set; }
		public virtual string Title { get; set; }

		/// <summary>
		/// You can only override this function if its a
		/// last drawing page
		/// </summary>
		public virtual string GenerateFullTitle()
		{
			return FormulaConfig.FullTitleGenerationFunc(this.Title);
		}

		public void SetScrollYExtraSpace(int pixels)
		{
			this.Context.AddPageData("ff_scrollyextraspace", pixels);
		}
	}
}
