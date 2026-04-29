namespace HngStageOne.Api.Helpers.Exceptions;

public class ProfileNotFoundException : Exception
{
    public ProfileNotFoundException(string message = "Profile not found") : base(message)
    {
    }
}
