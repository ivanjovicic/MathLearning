using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Infrastructure.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(DbContext db)
    {
        bool changed = false;

        // ── Categories ───────────────────────────────
        if (!await db.Set<Category>().AnyAsync())
        {
            db.Set<Category>().AddRange(
                new Category("Algebra"),
                new Category("Geometrija"),
                new Category("Aritmetika")
            );
            await db.SaveChangesAsync();
            changed = true;
        }

        // ── Topics & Subtopics ───────────────────────
        if (!await db.Set<Topic>().AnyAsync())
        {
            var t1 = new Topic("Osnove Algebre", "Jednačine, nejednačine i izrazi");
            var t2 = new Topic("Osnove Geometrije", "Osnovni geometrijski pojmovi");
            var t3 = new Topic("Sabiranje i Oduzimanje", "Osnovne aritmetičke operacije");
            db.Set<Topic>().AddRange(t1, t2, t3);
            await db.SaveChangesAsync();

            db.Set<Subtopic>().AddRange(
                new Subtopic("Jednačine", t1.Id),
                new Subtopic("Nejednačine", t1.Id),
                new Subtopic("Trouglovi", t2.Id),
                new Subtopic("Sabiranje do 100", t3.Id),
                new Subtopic("Množenje i Deljenje", t3.Id)
            );
            await db.SaveChangesAsync();
            changed = true;
        }

        // ── Questions ────────────────────────────────
        if (!await db.Set<Question>().AnyAsync())
        {
            var algebra = await db.Set<Category>().FirstAsync(c => c.Name == "Algebra");
            var geometrija = await db.Set<Category>().FirstAsync(c => c.Name == "Geometrija");
            var aritmetika = await db.Set<Category>().FirstAsync(c => c.Name == "Aritmetika");
            var subtopicJednacine = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Jednačine");
            var subtopicNejednacine = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Nejednačine");
            var subtopicTrouglovi = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Trouglovi");
            var subtopicSabiranje = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Sabiranje do 100");
            var subtopicMnozenjeDeljenje = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Množenje i Deljenje");
            if (subtopicMnozenjeDeljenje == null)
            {
                var topicAritmetika = await db.Set<Topic>().FirstAsync(t => t.Name == "Sabiranje i Oduzimanje");
                subtopicMnozenjeDeljenje = new Subtopic("Množenje i Deljenje", topicAritmetika.Id);
                db.Set<Subtopic>().Add(subtopicMnozenjeDeljenje);
                await db.SaveChangesAsync();
            }

            var questions = new List<Question>();

            // Algebra questions
            var q1 = new Question("Koliko je 2 + 2?", 1, algebra.Id, "2 + 2 = 4");
            q1.SetSubtopic(subtopicJednacine.Id);
            q1.ReplaceOptions(new List<QuestionOption>
            {
                new("4", true), new("3", false), new("5", false), new("6", false)
            });
            questions.Add(q1);

            var q2 = new Question("Reši: x + 5 = 12", 2, algebra.Id, "x = 12 - 5 = 7");
            q2.SetSubtopic(subtopicJednacine.Id);
            q2.SetHintFormula("x = 12 - 5");
            q2.SetHintClue("Oduzmi 5 sa obe strane");
            q2.ReplaceOptions(new List<QuestionOption>
            {
                new("7", true), new("6", false), new("8", false), new("5", false)
            });
            questions.Add(q2);

            var q3 = new Question("Reši: 2x = 10", 2, algebra.Id, "x = 10 / 2 = 5");
            q3.SetSubtopic(subtopicJednacine.Id);
            q3.SetHintFormula("x = 10 / 2");
            q3.SetHintClue("Podeli obe strane sa 2");
            q3.ReplaceOptions(new List<QuestionOption>
            {
                new("5", true), new("4", false), new("6", false), new("10", false)
            });
            questions.Add(q3);

            var q4 = new Question("Reši: 3x - 1 = 8", 3, algebra.Id, "3x = 9, x = 3");
            q4.SetSubtopic(subtopicJednacine.Id);
            q4.SetHintFormula("3x = 8 + 1");
            q4.SetHintClue("Dodaj 1 na obe strane, pa podeli sa 3");
            q4.ReplaceOptions(new List<QuestionOption>
            {
                new("3", true), new("2", false), new("4", false), new("1", false)
            });
            questions.Add(q4);

            // Aritmetika questions
            var q5 = new Question("Koliko je 15 + 27?", 1, aritmetika.Id, "15 + 27 = 42");
            q5.SetSubtopic(subtopicSabiranje.Id);
            q5.ReplaceOptions(new List<QuestionOption>
            {
                new("42", true), new("41", false), new("43", false), new("52", false)
            });
            questions.Add(q5);

            var q6 = new Question("Koliko je 48 + 35?", 1, aritmetika.Id, "48 + 35 = 83");
            q6.SetSubtopic(subtopicSabiranje.Id);
            q6.ReplaceOptions(new List<QuestionOption>
            {
                new("83", true), new("73", false), new("84", false), new("82", false)
            });
            questions.Add(q6);

            var q7 = new Question("Koliko je 99 - 47?", 2, aritmetika.Id, "99 - 47 = 52");
            q7.SetSubtopic(subtopicSabiranje.Id);
            q7.ReplaceOptions(new List<QuestionOption>
            {
                new("52", true), new("42", false), new("62", false), new("53", false)
            });
            questions.Add(q7);

            var q8 = new Question("Koliko je 9 × 6?", 2, aritmetika.Id, "9 × 6 = 54");
            q8.SetSubtopic(subtopicSabiranje.Id);
            q8.ReplaceOptions(new List<QuestionOption>
            {
                new("54", true), new("45", false), new("56", false), new("48", false)
            });
            questions.Add(q8);

            var q9 = new Question("Reši nejednačinu: x + 3 > 10", 2, algebra.Id, "x > 7");
            q9.SetSubtopic(subtopicNejednacine.Id);
            q9.SetHintFormula("x > 10 - 3");
            q9.SetHintClue("Izoluj x oduzimanjem 3 sa obe strane");
            q9.ReplaceOptions(new List<QuestionOption>
            {
                new("x > 7", true), new("x < 7", false), new("x = 7", false), new("x >= 7", false)
            });
            questions.Add(q9);

            var q10 = new Question("Reši nejednačinu: 2x ≤ 14", 3, algebra.Id, "x ≤ 7");
            q10.SetSubtopic(subtopicNejednacine.Id);
            q10.SetHintFormula("x ≤ 14 / 2");
            q10.SetHintClue("Podeli obe strane sa 2");
            q10.ReplaceOptions(new List<QuestionOption>
            {
                new("x ≤ 7", true), new("x ≥ 7", false), new("x < 7", false), new("x = 7", false)
            });
            questions.Add(q10);

            // Geometrija questions
            var q11 = new Question("Koliki je obim trougla sa stranicama 3, 4 i 5?", 1, geometrija.Id, "Obim je zbir svih stranica: 3 + 4 + 5 = 12");
            q11.SetSubtopic(subtopicTrouglovi.Id);
            q11.ReplaceOptions(new List<QuestionOption>
            {
                new("12", true), new("11", false), new("10", false), new("13", false)
            });
            questions.Add(q11);

            var q12 = new Question("Kolika je površina trougla sa osnovicom 10 i visinom 6?", 2, geometrija.Id, "P = (a × h) / 2 = (10 × 6) / 2 = 30");
            q12.SetSubtopic(subtopicTrouglovi.Id);
            q12.SetHintFormula("P = (a × h) / 2");
            q12.SetHintClue("Pomnoži osnovicu i visinu, pa podeli sa 2");
            q12.ReplaceOptions(new List<QuestionOption>
            {
                new("30", true), new("60", false), new("16", false), new("20", false)
            });
            questions.Add(q12);

            var q13 = new Question("Koliko je 84 ÷ 7?", 1, aritmetika.Id, "84 ÷ 7 = 12");
            q13.SetSubtopic(subtopicMnozenjeDeljenje.Id);
            q13.ReplaceOptions(new List<QuestionOption>
            {
                new("12", true), new("11", false), new("13", false), new("14", false)
            });
            questions.Add(q13);

            var q14 = new Question("Koliko je 36 ÷ 4 + 5?", 2, aritmetika.Id, "Prvo 36 ÷ 4 = 9, zatim 9 + 5 = 14");
            q14.SetSubtopic(subtopicMnozenjeDeljenje.Id);
            q14.SetHintFormula("36 ÷ 4 = 9");
            q14.SetHintClue("Poštuj redosled računskih operacija");
            q14.ReplaceOptions(new List<QuestionOption>
            {
                new("14", true), new("16", false), new("9", false), new("13", false)
            });
            questions.Add(q14);

            db.Set<Question>().AddRange(questions);
            await db.SaveChangesAsync();
            changed = true;

            // ── English translations ─────────────────
            var allQuestions = await db.Set<Question>().ToListAsync();

            var englishTranslations = new Dictionary<string, (string Text, string? Explanation, string? HintFormula, string? HintClue)>
            {
                ["Koliko je 2 + 2?"] = ("What is 2 + 2?", "2 + 2 = 4", null, null),
                ["Reši: x + 5 = 12"] = ("Solve: x + 5 = 12", "x = 12 - 5 = 7", "x = 12 - 5", "Subtract 5 from both sides"),
                ["Reši: 2x = 10"] = ("Solve: 2x = 10", "x = 10 / 2 = 5", "x = 10 / 2", "Divide both sides by 2"),
                ["Reši: 3x - 1 = 8"] = ("Solve: 3x - 1 = 8", "3x = 9, x = 3", "3x = 8 + 1", "Add 1 to both sides, then divide by 3"),
                ["Koliko je 15 + 27?"] = ("What is 15 + 27?", "15 + 27 = 42", null, null),
                ["Koliko je 48 + 35?"] = ("What is 48 + 35?", "48 + 35 = 83", null, null),
                ["Koliko je 99 - 47?"] = ("What is 99 - 47?", "99 - 47 = 52", null, null),
                ["Koliko je 9 × 6?"] = ("What is 9 × 6?", "9 × 6 = 54", null, null),
                ["Reši nejednačinu: x + 3 > 10"] = ("Solve the inequality: x + 3 > 10", "x > 7", "x > 10 - 3", "Isolate x by subtracting 3 from both sides"),
                ["Reši nejednačinu: 2x ≤ 14"] = ("Solve the inequality: 2x ≤ 14", "x ≤ 7", "x ≤ 14 / 2", "Divide both sides by 2"),
                ["Koliki je obim trougla sa stranicama 3, 4 i 5?"] = ("What is the perimeter of a triangle with sides 3, 4, and 5?", "Perimeter is the sum of all sides: 3 + 4 + 5 = 12", null, null),
                ["Kolika je površina trougla sa osnovicom 10 i visinom 6?"] = ("What is the area of a triangle with base 10 and height 6?", "A = (b × h) / 2 = (10 × 6) / 2 = 30", "A = (b × h) / 2", "Multiply base and height, then divide by 2"),
                ["Koliko je 84 ÷ 7?"] = ("What is 84 ÷ 7?", "84 ÷ 7 = 12", null, null),
                ["Koliko je 36 ÷ 4 + 5?"] = ("What is 36 ÷ 4 + 5?", "First 36 ÷ 4 = 9, then 9 + 5 = 14", "36 ÷ 4 = 9", "Follow operation precedence"),
            };

            foreach (var q in allQuestions)
            {
                if (englishTranslations.TryGetValue(q.Text, out var en))
                {
                    db.Set<QuestionTranslation>().Add(new QuestionTranslation(
                        q.Id, "en", en.Text, en.Explanation, en.HintFormula, en.HintClue));
                }
            }

            await db.SaveChangesAsync();

            // ── Stored step-by-step examples (with English translations) ──
            var stepDefinitions = new Dictionary<string, List<(string SrText, string? SrHint, bool Highlight, string EnText, string? EnHint)>>
            {
                ["Reši nejednačinu: x + 3 > 10"] = new()
                {
                    ("Početna nejednačina: x + 3 > 10", null, false, "Start with the inequality: x + 3 > 10", null),
                    ("Oduzmi 3 sa obe strane: x + 3 - 3 > 10 - 3", "Ista operacija ide na obe strane", false, "Subtract 3 from both sides: x + 3 - 3 > 10 - 3", "Apply the same operation to both sides"),
                    ("Dobijamo: x > 7", null, true, "We get: x > 7", null),
                },
                ["Koliki je obim trougla sa stranicama 3, 4 i 5?"] = new()
                {
                    ("Formula za obim trougla je O = a + b + c", null, false, "Triangle perimeter formula is P = a + b + c", null),
                    ("Uvrsti stranice: O = 3 + 4 + 5", null, false, "Substitute side lengths: P = 3 + 4 + 5", null),
                    ("Izračunaj zbir: O = 12", null, true, "Calculate the sum: P = 12", null),
                },
                ["Kolika je površina trougla sa osnovicom 10 i visinom 6?"] = new()
                {
                    ("Formula za površinu trougla je P = (a × h) / 2", null, false, "Triangle area formula is A = (b × h) / 2", null),
                    ("Uvrsti vrednosti: P = (10 × 6) / 2", null, false, "Substitute values: A = (10 × 6) / 2", null),
                    ("Izračunaj: P = 60 / 2 = 30", null, true, "Compute: A = 60 / 2 = 30", null),
                },
                ["Koliko je 36 ÷ 4 + 5?"] = new()
                {
                    ("Prvo radi deljenje: 36 ÷ 4 = 9", "Deljenje ima prioritet nad sabiranjem", false, "Do division first: 36 ÷ 4 = 9", "Division has higher precedence than addition"),
                    ("Zatim saberi: 9 + 5 = 14", null, false, "Then add: 9 + 5 = 14", null),
                    ("Konačan rezultat je 14", null, true, "Final result is 14", null),
                },
            };

            var stepsToTranslate = new List<(QuestionStep Step, string EnText, string? EnHint)>();
            foreach (var q in allQuestions)
            {
                if (!stepDefinitions.TryGetValue(q.Text, out var steps))
                    continue;

                for (int i = 0; i < steps.Count; i++)
                {
                    var step = new QuestionStep(q.Id, i + 1, steps[i].SrText, steps[i].SrHint, steps[i].Highlight);
                    db.Set<QuestionStep>().Add(step);
                    stepsToTranslate.Add((step, steps[i].EnText, steps[i].EnHint));
                }
            }

            if (stepsToTranslate.Count > 0)
            {
                await db.SaveChangesAsync();

                var stepTranslationsToAdd = stepsToTranslate
                    .Select(x => new QuestionStepTranslation(
                        x.Step.Id,
                        "en",
                        x.EnText,
                        x.EnHint))
                    .ToList();

                db.Set<QuestionStepTranslation>().AddRange(stepTranslationsToAdd);
                await db.SaveChangesAsync();
            }
        }

        // ── Ensure additional examples for existing databases (idempotent) ──
        var algebraCategory = await db.Set<Category>().FirstAsync(c => c.Name == "Algebra");
        var geometrijaCategory = await db.Set<Category>().FirstAsync(c => c.Name == "Geometrija");
        var aritmetikaCategory = await db.Set<Category>().FirstAsync(c => c.Name == "Aritmetika");

        var topicAritmetikaExisting = await db.Set<Topic>().FirstAsync(t => t.Name == "Sabiranje i Oduzimanje");

        var subtopicNejednacineExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Nejednačine");
        if (subtopicNejednacineExisting == null)
        {
            subtopicNejednacineExisting = new Subtopic("Nejednačine", (await db.Set<Topic>().FirstAsync(t => t.Name == "Osnove Algebre")).Id);
            db.Set<Subtopic>().Add(subtopicNejednacineExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicTrougloviExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Trouglovi");
        if (subtopicTrougloviExisting == null)
        {
            subtopicTrougloviExisting = new Subtopic("Trouglovi", (await db.Set<Topic>().FirstAsync(t => t.Name == "Osnove Geometrije")).Id);
            db.Set<Subtopic>().Add(subtopicTrougloviExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicMnozenjeDeljenjeExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Množenje i Deljenje");
        if (subtopicMnozenjeDeljenjeExisting == null)
        {
            subtopicMnozenjeDeljenjeExisting = new Subtopic("Množenje i Deljenje", topicAritmetikaExisting.Id);
            db.Set<Subtopic>().Add(subtopicMnozenjeDeljenjeExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var ensureQuestionDefinitions = new List<(
            string Text,
            int Difficulty,
            int CategoryId,
            int SubtopicId,
            string? Explanation,
            string? HintFormula,
            string? HintClue,
            List<(string OptionText, bool IsCorrect)> Options)>
        {
            (
                "Reši nejednačinu: x + 3 > 10",
                2,
                algebraCategory.Id,
                subtopicNejednacineExisting.Id,
                "x > 7",
                "x > 10 - 3",
                "Izoluj x oduzimanjem 3 sa obe strane",
                new() { ("x > 7", true), ("x < 7", false), ("x = 7", false), ("x >= 7", false) }
            ),
            (
                "Reši nejednačinu: 2x ≤ 14",
                3,
                algebraCategory.Id,
                subtopicNejednacineExisting.Id,
                "x ≤ 7",
                "x ≤ 14 / 2",
                "Podeli obe strane sa 2",
                new() { ("x ≤ 7", true), ("x ≥ 7", false), ("x < 7", false), ("x = 7", false) }
            ),
            (
                "Koliki je obim trougla sa stranicama 3, 4 i 5?",
                1,
                geometrijaCategory.Id,
                subtopicTrougloviExisting.Id,
                "Obim je zbir svih stranica: 3 + 4 + 5 = 12",
                null,
                null,
                new() { ("12", true), ("11", false), ("10", false), ("13", false) }
            ),
            (
                "Kolika je površina trougla sa osnovicom 10 i visinom 6?",
                2,
                geometrijaCategory.Id,
                subtopicTrougloviExisting.Id,
                "P = (a × h) / 2 = (10 × 6) / 2 = 30",
                "P = (a × h) / 2",
                "Pomnoži osnovicu i visinu, pa podeli sa 2",
                new() { ("30", true), ("60", false), ("16", false), ("20", false) }
            ),
            (
                "Koliko je 84 ÷ 7?",
                1,
                aritmetikaCategory.Id,
                subtopicMnozenjeDeljenjeExisting.Id,
                "84 ÷ 7 = 12",
                null,
                null,
                new() { ("12", true), ("11", false), ("13", false), ("14", false) }
            ),
            (
                "Koliko je 36 ÷ 4 + 5?",
                2,
                aritmetikaCategory.Id,
                subtopicMnozenjeDeljenjeExisting.Id,
                "Prvo 36 ÷ 4 = 9, zatim 9 + 5 = 14",
                "36 ÷ 4 = 9",
                "Poštuj redosled računskih operacija",
                new() { ("14", true), ("16", false), ("9", false), ("13", false) }
            )
        };

        foreach (var qd in ensureQuestionDefinitions)
        {
            var existing = await db.Set<Question>()
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Text == qd.Text);
            if (existing != null)
                continue;

            var q = new Question(qd.Text, qd.Difficulty, qd.CategoryId, qd.Explanation);
            q.SetSubtopic(qd.SubtopicId);
            q.SetHintFormula(qd.HintFormula);
            q.SetHintClue(qd.HintClue);
            q.ReplaceOptions(qd.Options.Select(o => new QuestionOption(o.OptionText, o.IsCorrect)));

            db.Set<Question>().Add(q);
            changed = true;
        }

        await db.SaveChangesAsync();

        // Repair existing known questions that may have been created without options.
        // Some older mobile/admin flows could leave questions with empty option sets.
        var canonicalOptionsByText = new Dictionary<string, List<(string OptionText, bool IsCorrect)>>
        {
            ["Koliko je 2 + 2?"] = new() { ("4", true), ("3", false), ("5", false), ("6", false) },
            ["Reši: x + 5 = 12"] = new() { ("7", true), ("6", false), ("8", false), ("5", false) },
            ["Reši: 2x = 10"] = new() { ("5", true), ("4", false), ("6", false), ("10", false) },
            ["Reši: 3x - 1 = 8"] = new() { ("3", true), ("2", false), ("4", false), ("1", false) },
            ["Koliko je 15 + 27?"] = new() { ("42", true), ("41", false), ("43", false), ("52", false) },
            ["Koliko je 48 + 35?"] = new() { ("83", true), ("73", false), ("84", false), ("82", false) },
            ["Koliko je 99 - 47?"] = new() { ("52", true), ("42", false), ("62", false), ("53", false) },
            ["Koliko je 9 × 6?"] = new() { ("54", true), ("45", false), ("56", false), ("48", false) },
            ["Reši nejednačinu: x + 3 > 10"] = new() { ("x > 7", true), ("x < 7", false), ("x = 7", false), ("x >= 7", false) },
            ["Reši nejednačinu: 2x ≤ 14"] = new() { ("x ≤ 7", true), ("x ≥ 7", false), ("x < 7", false), ("x = 7", false) },
            ["Koliki je obim trougla sa stranicama 3, 4 i 5?"] = new() { ("12", true), ("11", false), ("10", false), ("13", false) },
            ["Kolika je površina trougla sa osnovicom 10 i visinom 6?"] = new() { ("30", true), ("60", false), ("16", false), ("20", false) },
            ["Koliko je 84 ÷ 7?"] = new() { ("12", true), ("11", false), ("13", false), ("14", false) },
            ["Koliko je 36 ÷ 4 + 5?"] = new() { ("14", true), ("16", false), ("9", false), ("13", false) },
        };

        var maybeBrokenQuestions = await db.Set<Question>()
            .Include(q => q.Options)
            .Where(q => canonicalOptionsByText.Keys.Contains(q.Text))
            .ToListAsync();

        foreach (var question in maybeBrokenQuestions)
        {
            if (question.Options.Count > 0)
                continue;
            if (!canonicalOptionsByText.TryGetValue(question.Text, out var canonicalOptions))
                continue;

            foreach (var opt in canonicalOptions)
            {
                question.Options.Add(new QuestionOption(opt.OptionText, opt.IsCorrect));
            }
            changed = true;
        }

        await db.SaveChangesAsync();

        var ensureEnglishTranslations = new Dictionary<string, (string Text, string? Explanation, string? HintFormula, string? HintClue)>
        {
            ["Reši nejednačinu: x + 3 > 10"] = ("Solve the inequality: x + 3 > 10", "x > 7", "x > 10 - 3", "Isolate x by subtracting 3 from both sides"),
            ["Reši nejednačinu: 2x ≤ 14"] = ("Solve the inequality: 2x ≤ 14", "x ≤ 7", "x ≤ 14 / 2", "Divide both sides by 2"),
            ["Koliki je obim trougla sa stranicama 3, 4 i 5?"] = ("What is the perimeter of a triangle with sides 3, 4, and 5?", "Perimeter is the sum of all sides: 3 + 4 + 5 = 12", null, null),
            ["Kolika je površina trougla sa osnovicom 10 i visinom 6?"] = ("What is the area of a triangle with base 10 and height 6?", "A = (b × h) / 2 = (10 × 6) / 2 = 30", "A = (b × h) / 2", "Multiply base and height, then divide by 2"),
            ["Koliko je 84 ÷ 7?"] = ("What is 84 ÷ 7?", "84 ÷ 7 = 12", null, null),
            ["Koliko je 36 ÷ 4 + 5?"] = ("What is 36 ÷ 4 + 5?", "First 36 ÷ 4 = 9, then 9 + 5 = 14", "36 ÷ 4 = 9", "Follow operation precedence"),
        };

        var ensureQuestions = await db.Set<Question>()
            .Where(q => ensureEnglishTranslations.Keys.Contains(q.Text))
            .ToListAsync();

        foreach (var q in ensureQuestions)
        {
            if (!ensureEnglishTranslations.TryGetValue(q.Text, out var en))
                continue;

            bool hasEn = await db.Set<QuestionTranslation>()
                .AnyAsync(t => t.QuestionId == q.Id && t.Lang == "en");
            if (hasEn)
                continue;

            db.Set<QuestionTranslation>().Add(new QuestionTranslation(
                q.Id, "en", en.Text, en.Explanation, en.HintFormula, en.HintClue));
            changed = true;
        }

        await db.SaveChangesAsync();

        var ensureStepDefinitions = new Dictionary<string, List<(string SrText, string? SrHint, bool Highlight, string EnText, string? EnHint)>>
        {
            ["Reši nejednačinu: x + 3 > 10"] = new()
            {
                ("Početna nejednačina: x + 3 > 10", null, false, "Start with the inequality: x + 3 > 10", null),
                ("Oduzmi 3 sa obe strane: x + 3 - 3 > 10 - 3", "Ista operacija ide na obe strane", false, "Subtract 3 from both sides: x + 3 - 3 > 10 - 3", "Apply the same operation to both sides"),
                ("Dobijamo: x > 7", null, true, "We get: x > 7", null),
            },
            ["Koliki je obim trougla sa stranicama 3, 4 i 5?"] = new()
            {
                ("Formula za obim trougla je O = a + b + c", null, false, "Triangle perimeter formula is P = a + b + c", null),
                ("Uvrsti stranice: O = 3 + 4 + 5", null, false, "Substitute side lengths: P = 3 + 4 + 5", null),
                ("Izračunaj zbir: O = 12", null, true, "Calculate the sum: P = 12", null),
            },
            ["Kolika je površina trougla sa osnovicom 10 i visinom 6?"] = new()
            {
                ("Formula za površinu trougla je P = (a × h) / 2", null, false, "Triangle area formula is A = (b × h) / 2", null),
                ("Uvrsti vrednosti: P = (10 × 6) / 2", null, false, "Substitute values: A = (10 × 6) / 2", null),
                ("Izračunaj: P = 60 / 2 = 30", null, true, "Compute: A = 60 / 2 = 30", null),
            },
            ["Koliko je 36 ÷ 4 + 5?"] = new()
            {
                ("Prvo radi deljenje: 36 ÷ 4 = 9", "Deljenje ima prioritet nad sabiranjem", false, "Do division first: 36 ÷ 4 = 9", "Division has higher precedence than addition"),
                ("Zatim saberi: 9 + 5 = 14", null, false, "Then add: 9 + 5 = 14", null),
                ("Konačan rezultat je 14", null, true, "Final result is 14", null),
            },
        };

        foreach (var q in ensureQuestions)
        {
            if (!ensureStepDefinitions.TryGetValue(q.Text, out var steps))
                continue;

            var existingSteps = await db.Set<QuestionStep>()
                .Where(s => s.QuestionId == q.Id)
                .OrderBy(s => s.StepIndex)
                .ToListAsync();

            if (existingSteps.Count == 0)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    db.Set<QuestionStep>().Add(new QuestionStep(
                        q.Id,
                        i + 1,
                        steps[i].SrText,
                        steps[i].SrHint,
                        steps[i].Highlight));
                }
                changed = true;
                await db.SaveChangesAsync();

                existingSteps = await db.Set<QuestionStep>()
                    .Where(s => s.QuestionId == q.Id)
                    .OrderBy(s => s.StepIndex)
                    .ToListAsync();
            }

            for (int i = 0; i < existingSteps.Count && i < steps.Count; i++)
            {
                bool hasEn = await db.Set<QuestionStepTranslation>()
                    .AnyAsync(t => t.QuestionStepId == existingSteps[i].Id && t.Lang == "en");
                if (hasEn)
                    continue;

                db.Set<QuestionStepTranslation>().Add(new QuestionStepTranslation(
                    existingSteps[i].Id,
                    "en",
                    steps[i].EnText,
                    steps[i].EnHint));
                changed = true;
            }
        }

        await db.SaveChangesAsync();

        // ── LaTeX test questions (idempotent) ─────────
        var latexAlgebraCategory = await db.Set<Category>().FirstOrDefaultAsync(c => c.Name == "Algebra");
        var latexEquationsSubtopic = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Jednačine");

        if (latexAlgebraCategory != null && latexEquationsSubtopic != null)
        {
            var latexQuestionSeeds = new[]
            {
                new
                {
                    Text = "LaTeX test: Izračunaj derivaciju funkcije $f(x)=x^3-2x$ za $x=2$.",
                    Explanation = "Derivacija je $f'(x)=3x^2-2$, pa je $f'(2)=3\\cdot 4-2=10$.",
                    HintFormula = "Koristi pravilo: $(x^n)' = n x^{n-1}$ i $(ax)'=a$.",
                    HintClue = "Prvo izračunaj $f'(x)$, pa zatim uvrsti $x=2$.",
                    Difficulty = 2,
                    Options = new List<QuestionOption>
                    {
                        new("10", true), new("8", false), new("12", false), new("6", false)
                    }
                },
                new
                {
                    Text = "LaTeX test: Izračunaj određeni integral $\\int_0^1 (2x+1)\\,dx$.",
                    Explanation = "Primitivna funkcija je $x^2+x$, pa je $\\left[x^2+x\\right]_0^1=(1+1)-0=2$.",
                    HintFormula = "Koristi: $\\int (2x+1)\\,dx = x^2 + x + C$.",
                    HintClue = "Nađi primitivnu funkciju i izračunaj $F(1)-F(0)$.",
                    Difficulty = 3,
                    Options = new List<QuestionOption>
                    {
                        new("2", true), new("1", false), new("3", false), new("4", false)
                    }
                },
                new
                {
                    Text = "LaTeX test: Ako su $f(x)=2x+3$ i $g(x)=x^2$, izračunaj $(f\\circ g)(2)$.",
                    Explanation = "Prvo $g(2)=4$, zatim $f(4)=2\\cdot 4+3=11$.",
                    HintFormula = "$(f\\circ g)(x)=f(g(x))$.",
                    HintClue = "Prvo izračunaj unutrašnju funkciju $g(2)$, pa rezultat ubaci u $f$.",
                    Difficulty = 2,
                    Options = new List<QuestionOption>
                    {
                        new("11", true), new("7", false), new("9", false), new("13", false)
                    }
                }
            };

            var existingTexts = await db.Set<Question>()
                .Select(q => q.Text)
                .ToListAsync();

            var latexQuestionsToInsert = new List<Question>();

            foreach (var seed in latexQuestionSeeds)
            {
                if (existingTexts.Contains(seed.Text))
                    continue;

                var q = new Question(seed.Text, seed.Difficulty, latexAlgebraCategory.Id, seed.Explanation);
                q.SetSubtopic(latexEquationsSubtopic.Id);
                q.SetHintFormula(seed.HintFormula);
                q.SetHintClue(seed.HintClue);
                q.ReplaceOptions(seed.Options);
                latexQuestionsToInsert.Add(q);
            }

            if (latexQuestionsToInsert.Count > 0)
            {
                db.Set<Question>().AddRange(latexQuestionsToInsert);
                await db.SaveChangesAsync();
                changed = true;
            }

            var latexTranslations = new Dictionary<string, (string Text, string Explanation, string HintLight, string HintMedium, string HintFull)>
            {
                ["LaTeX test: Izračunaj derivaciju funkcije $f(x)=x^3-2x$ za $x=2$."] =
                    ("LaTeX test: Compute the derivative of $f(x)=x^3-2x$ at $x=2$.",
                     "The derivative is $f'(x)=3x^2-2$, so $f'(2)=3\\cdot 4-2=10$.",
                     "Use $(x^n)' = n x^{n-1}$ and $(ax)'=a$.",
                     "First compute $f'(x)$, then substitute $x=2$.",
                     "Step 1: $f'(x)=3x^2-2$. Step 2: $f'(2)=3\\cdot 4-2=10$."),

                ["LaTeX test: Izračunaj određeni integral $\\int_0^1 (2x+1)\\,dx$."] =
                    ("LaTeX test: Compute the definite integral $\\int_0^1 (2x+1)\\,dx$.",
                     "An antiderivative is $x^2+x$, so $\\left[x^2+x\\right]_0^1=(1+1)-0=2$.",
                     "Use $\\int (2x+1)\\,dx = x^2 + x + C$.",
                     "Find an antiderivative and evaluate $F(1)-F(0)$.",
                     "Step 1: $F(x)=x^2+x$. Step 2: $F(1)=2$, $F(0)=0$. Step 3: result is $2$."),

                ["LaTeX test: Ako su $f(x)=2x+3$ i $g(x)=x^2$, izračunaj $(f\\circ g)(2)$."] =
                    ("LaTeX test: If $f(x)=2x+3$ and $g(x)=x^2$, compute $(f\\circ g)(2)$.",
                     "First $g(2)=4$, then $f(4)=2\\cdot 4+3=11$.",
                     "Use $(f\\circ g)(x)=f(g(x))$.",
                     "Compute the inner function first: $g(2)$.",
                     "Step 1: $g(2)=4$. Step 2: $f(4)=2\\cdot 4+3=11$.")
            };

            var latexTexts = latexTranslations.Keys.ToList();
            var latexQuestions = await db.Set<Question>()
                .Where(q => latexTexts.Contains(q.Text))
                .ToListAsync();

            var existingEnglishTranslationIds = (await db.Set<QuestionTranslation>()
                .Where(t => t.Lang == "en")
                .Select(t => t.QuestionId)
                .ToListAsync())
                .ToHashSet();

            var translationsToInsert = new List<QuestionTranslation>();

            foreach (var q in latexQuestions)
            {
                if (!latexTranslations.TryGetValue(q.Text, out var translation))
                    continue;
                if (existingEnglishTranslationIds.Contains(q.Id))
                    continue;

                translationsToInsert.Add(new QuestionTranslation(
                    q.Id,
                    "en",
                    translation.Text,
                    translation.Explanation,
                    translation.HintLight,
                    translation.HintMedium,
                    translation.HintLight,
                    translation.HintMedium,
                    translation.HintFull));
            }

            if (translationsToInsert.Count > 0)
            {
                db.Set<QuestionTranslation>().AddRange(translationsToInsert);
                await db.SaveChangesAsync();
                changed = true;
            }
        }

        // ── Demo user profiles ───────────────────────
        if (!await db.Set<UserProfile>().AnyAsync())
        {
            db.Set<UserProfile>().AddRange(
                new UserProfile
                {
                    UserId = 1,
                    Username = "admin",
                    DisplayName = "Admin",
                    Coins = 500,
                    Level = 5,
                    Xp = 420,
                    Streak = 7,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new UserProfile
                {
                    UserId = 2,
                    Username = "demo",
                    DisplayName = "Demo User",
                    Coins = 100,
                    Level = 1,
                    Xp = 0,
                    Streak = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
            changed = true;
        }

        if (changed)
        {
            Console.WriteLine("✓ Database seeded with initial data");
        }
    }
}
