namespace MathLearning.Domain.Entities;

public class StepExplanationTemplate
{
    public Guid Id { get; private set; }
    public string RuleKey { get; private set; } = "";
    public string Language { get; private set; } = "sr";
    public string StepType { get; private set; } = "";
    public string TemplateText { get; private set; } = "";
    public string? HintTemplate { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private StepExplanationTemplate() { }

    public StepExplanationTemplate(string ruleKey, string language, string stepType, string templateText, string? hintTemplate = null)
    {
        Id = Guid.NewGuid();
        SetRuleKey(ruleKey);
        SetLanguage(language);
        SetStepType(stepType);
        SetTemplateText(templateText);
        SetHintTemplate(hintTemplate);
    }

    public void SetRuleKey(string ruleKey)
    {
        RuleKey = string.IsNullOrWhiteSpace(ruleKey) ? throw new ArgumentException("Rule key is required.", nameof(ruleKey)) : ruleKey.Trim();
        Touch();
    }

    public void SetLanguage(string language)
    {
        Language = string.IsNullOrWhiteSpace(language) ? throw new ArgumentException("Language is required.", nameof(language)) : language.Trim().ToLowerInvariant();
        Touch();
    }

    public void SetStepType(string stepType)
    {
        StepType = string.IsNullOrWhiteSpace(stepType) ? throw new ArgumentException("Step type is required.", nameof(stepType)) : stepType.Trim();
        Touch();
    }

    public void SetTemplateText(string templateText)
    {
        TemplateText = string.IsNullOrWhiteSpace(templateText) ? throw new ArgumentException("Template text is required.", nameof(templateText)) : templateText.Trim();
        Touch();
    }

    public void SetHintTemplate(string? hintTemplate)
    {
        HintTemplate = string.IsNullOrWhiteSpace(hintTemplate) ? null : hintTemplate.Trim();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
