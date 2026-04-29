namespace HngStageOne.Api.Helpers;

public static class AgeGroupClassifier
{
    public static string Classify(int age)
    {
        return age switch
        {
            >= 0 and <= 12 => "child",
            >= 13 and <= 19 => "teenager",
            >= 20 and <= 59 => "adult",
            >= 60 => "senior",
            _ => "unknown"
        };
    }
}
