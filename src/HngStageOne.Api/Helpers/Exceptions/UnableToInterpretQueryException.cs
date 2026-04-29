namespace HngStageOne.Api.Helpers.Exceptions;

public class UnableToInterpretQueryException : Exception
{
    public UnableToInterpretQueryException()
        : base("Unable to interpret query")
    {
    }
}
