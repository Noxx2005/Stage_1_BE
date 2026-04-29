namespace HngStageOne.Api.Helpers.Exceptions;

public class MissingOrEmptyParameterException : Exception
{
    public MissingOrEmptyParameterException()
        : base("Missing or empty parameter")
    {
    }
}
