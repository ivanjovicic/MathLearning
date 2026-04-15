namespace MathLearning.Admin.Services;

public static class DeleteImpactFormatter
{
    public static string BuildCategoryDeleteMessage(string categoryName, int dependentQuestionsCount)
    {
        if (dependentQuestionsCount <= 0)
        {
            return $"Delete category '{categoryName}' permanently?";
        }

        return $"Category '{categoryName}' contains {dependentQuestionsCount} question(s). " +
               "Delete is blocked until questions are moved or removed.";
    }

    public static string BuildQuestionDeleteMessage(int questionId, int optionsCount, int stepsCount)
    {
        return $"Delete question #{questionId} permanently? This also removes {optionsCount} option(s) and {stepsCount} step(s).";
    }
}
