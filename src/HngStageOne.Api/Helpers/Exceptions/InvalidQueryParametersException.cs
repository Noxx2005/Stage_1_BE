namespace HngStageOne.Api.Helpers.Exceptions;

public class InvalidQueryParametersException : Exception
{
    public InvalidQueryParametersException()
        : base("Invalid query parameters")
    {
    }
}
