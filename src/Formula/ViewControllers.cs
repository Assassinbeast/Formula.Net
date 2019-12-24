using System.Threading.Tasks;

namespace Formula
{
	public abstract class ViewController
	{
		public FormulaContext Context;
		public bool ShallDraw;

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
	public abstract class BaseLayout : ViewController { }
	public abstract class BasePage : ViewController
	{
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
			if (this.Context.IsFirstPageLoad == false)
				this.Context.AddRData("ff_scrollyextraspace", pixels);
		}
	}
}
