using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.TranslationJob.Services;

public class QuestionTranslationJob
{
    private readonly ApiDbContext _db;
    private readonly ITranslationClient _translator;

    public QuestionTranslationJob(ApiDbContext db, ITranslationClient translator)
    {
        _db = db;
        _translator = translator;
    }

    public async Task GenerateMissingTranslationsAsync(string targetLang, string baseLang = "en")
    {
        // 1) Find questions without translation in targetLang
        var questions = await _db.Questions
            .Include(q => q.Translations)
            .Where(q => q.Translations.All(t => t.Lang != targetLang))
            .ToListAsync();

        Console.WriteLine($"Found {questions.Count} questions without '{targetLang}' translation.");

        int processed = 0;
        int errors = 0;

        foreach (var q in questions)
        {
            try
            {
                // 2) Get base text (prefer existing translation, fallback to Question.Text)
                var baseTranslation = q.Translations.FirstOrDefault(t => t.Lang == baseLang);
                var baseText = baseTranslation?.Text ?? q.Text;

                if (string.IsNullOrWhiteSpace(baseText))
                {
                    Console.WriteLine($"Skipping Q={q.Id}: no base text in '{baseLang}'");
                    continue;
                }

                // 3) Translate the text
                var translatedText = await _translator.TranslateTextAsync(baseText, baseLang, targetLang);

                // 4) Translate hints if they exist
                string? translatedHintFormula = null;
                string? translatedHintClue = null;
                string? translatedHintLight = null;
                string? translatedHintMedium = null;
                string? translatedHintFull = null;
                string? translatedExplanation = null;

                if (!string.IsNullOrWhiteSpace(baseTranslation?.HintFormula))
                {
                    translatedHintFormula = await _translator.TranslateTextAsync(baseTranslation.HintFormula, baseLang, targetLang);
                }

                if (!string.IsNullOrWhiteSpace(baseTranslation?.HintClue))
                {
                    translatedHintClue = await _translator.TranslateTextAsync(baseTranslation.HintClue, baseLang, targetLang);
                }

                if (!string.IsNullOrWhiteSpace(baseTranslation?.HintLight))
                {
                    translatedHintLight = await _translator.TranslateTextAsync(baseTranslation.HintLight, baseLang, targetLang);
                }

                if (!string.IsNullOrWhiteSpace(baseTranslation?.HintMedium))
                {
                    translatedHintMedium = await _translator.TranslateTextAsync(baseTranslation.HintMedium, baseLang, targetLang);
                }

                if (!string.IsNullOrWhiteSpace(baseTranslation?.HintFull))
                {
                    translatedHintFull = await _translator.TranslateTextAsync(baseTranslation.HintFull, baseLang, targetLang);
                }

                if (!string.IsNullOrWhiteSpace(baseTranslation?.Explanation))
                {
                    translatedExplanation = await _translator.TranslateTextAsync(baseTranslation.Explanation, baseLang, targetLang);
                }

                // 5) Create and save the translation
                var translation = new QuestionTranslation(
                    q.Id,
                    targetLang,
                    translatedText,
                    translatedExplanation,
                    translatedHintFormula,
                    translatedHintClue,
                    translatedHintLight,
                    translatedHintMedium,
                    translatedHintFull
                );

                _db.QuestionTranslations.Add(translation);
                await _db.SaveChangesAsync();

                processed++;
                Console.WriteLine($"✓ Translated Q={q.Id} → {targetLang}");

                // Rate limiting - avoid hitting API limits
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"✗ Error translating Q={q.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nCompleted: {processed} translated, {errors} errors");
    }

    public async Task GenerateOptionTranslationsAsync(string targetLang, string baseLang = "en")
    {
        // Find options without translation in targetLang
        var options = await _db.Options
            .Include(o => o.Translations)
            .Where(o => o.Translations.All(t => t.Lang != targetLang))
            .ToListAsync();

        Console.WriteLine($"Found {options.Count} options without '{targetLang}' translation.");

        int processed = 0;
        int errors = 0;

        foreach (var option in options)
        {
            try
            {
                // Get base text
                var baseTranslation = option.Translations.FirstOrDefault(t => t.Lang == baseLang);
                var baseText = baseTranslation?.Text ?? option.Text;

                if (string.IsNullOrWhiteSpace(baseText))
                {
                    Console.WriteLine($"Skipping Option={option.Id}: no base text in '{baseLang}'");
                    continue;
                }

                // Translate
                var translatedText = await _translator.TranslateTextAsync(baseText, baseLang, targetLang);

                // Create translation
                var translation = new OptionTranslation(option.Id, targetLang, translatedText);
                _db.OptionTranslations.Add(translation);
                await _db.SaveChangesAsync();

                processed++;
                Console.WriteLine($"✓ Translated Option={option.Id} → {targetLang}");

                await Task.Delay(50); // Rate limiting
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"✗ Error translating Option={option.Id}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nCompleted: {processed} translated, {errors} errors");
    }

    public async Task FillMissingHintFieldsAsync(string targetLang, string baseLang = "en")
    {
        // Find translations in targetLang where new hint fields are NULL
        var missingHints = await _db.QuestionTranslations
            .Include(t => t.Question)
                .ThenInclude(q => q.Translations)
            .Where(t => t.Lang == targetLang &&
                       (t.HintLight == null || t.HintMedium == null || t.HintFull == null))
            .ToListAsync();

        Console.WriteLine($"Found {missingHints.Count} translations in '{targetLang}' with missing hint fields.");

        int processed = 0;
        int errors = 0;

        foreach (var tr in missingHints)
        {
            try
            {
                // Get base translation for hints
                var baseTranslation = tr.Question.Translations.FirstOrDefault(t => t.Lang == baseLang);

                if (baseTranslation == null)
                {
                    Console.WriteLine($"Skipping Q={tr.QuestionId}: no base translation in '{baseLang}'");
                    continue;
                }

                // Translate missing hint fields
                if (tr.HintLight == null && !string.IsNullOrWhiteSpace(baseTranslation.HintLight))
                {
                    tr.SetHintLight(await _translator.TranslateTextAsync(baseTranslation.HintLight, baseLang, targetLang));
                    Console.WriteLine($"✓ Translated HintLight for Q={tr.QuestionId}");
                }

                if (tr.HintMedium == null && !string.IsNullOrWhiteSpace(baseTranslation.HintMedium))
                {
                    tr.SetHintMedium(await _translator.TranslateTextAsync(baseTranslation.HintMedium, baseLang, targetLang));
                    Console.WriteLine($"✓ Translated HintMedium for Q={tr.QuestionId}");
                }

                if (tr.HintFull == null && !string.IsNullOrWhiteSpace(baseTranslation.HintFull))
                {
                    tr.SetHintFull(await _translator.TranslateTextAsync(baseTranslation.HintFull, baseLang, targetLang));
                    Console.WriteLine($"✓ Translated HintFull for Q={tr.QuestionId}");
                }

                await _db.SaveChangesAsync();
                processed++;

                // Rate limiting
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"✗ Error filling hints for Q={tr.QuestionId}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nCompleted: {processed} updated, {errors} errors");
    }
}
