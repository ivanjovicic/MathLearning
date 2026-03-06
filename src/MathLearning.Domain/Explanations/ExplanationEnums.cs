namespace MathLearning.Domain.Explanations;

public enum StepType
{
    Intro = 1,
    Formula = 2,
    Transformation = 3,
    Calculation = 4,
    Simplification = 5,
    Visualization = 6,
    FinalResult = 7,
    MistakeExplanation = 8
}

public enum ExplanationType
{
    Normal = 1,
    Hint = 2,
    MistakeCorrection = 3,
    ConceptClarification = 4
}

public enum DifficultyLevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Advanced = 4
}

public enum HintType
{
    General = 1,
    Formula = 2,
    NextStep = 3,
    Concept = 4,
    Warning = 5,
    Strategy = 6
}

public enum CommonMistakeType
{
    None = 0,
    FractionDenominatorAddition = 1,
    SignError = 2,
    IncorrectFormulaUsage = 3,
    ArithmeticSlip = 4,
    OrderOfOperations = 5,
    Unknown = 99
}

public enum ReasoningRule
{
    ParseProblem = 1,
    AddFractions = 2,
    AddNumerators = 3,
    SimplifyFraction = 4,
    DistributeMultiplication = 5,
    IsolateVariable = 6,
    ApplyFormula = 7,
    EvaluateArithmetic = 8,
    NormalizeEquation = 9,
    FinalizeResult = 10,
    Unknown = 99
}
