using System;

namespace Formula.Exceptions
{
    public class ProcessLogicTypeConversionErrorException: Exception
    {
		public Type TypeThatCantBeConvertedFromString;
		public ProcessLogicTypeConversionErrorException(Type type)
		{
			this.TypeThatCantBeConvertedFromString = type;
		}
	}
}
