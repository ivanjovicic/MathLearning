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

        var topicAlgebraExisting = await db.Set<Topic>().FirstAsync(t => t.Name == "Osnove Algebre");
        var topicGeometrijaExisting = await db.Set<Topic>().FirstAsync(t => t.Name == "Osnove Geometrije");
        var topicAritmetikaExisting = await db.Set<Topic>().FirstAsync(t => t.Name == "Sabiranje i Oduzimanje");

        var subtopicJednacineExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Jednačine");
        if (subtopicJednacineExisting == null)
        {
            subtopicJednacineExisting = new Subtopic("Jednačine", topicAlgebraExisting.Id);
            db.Set<Subtopic>().Add(subtopicJednacineExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicNejednacineExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Nejednačine");
        if (subtopicNejednacineExisting == null)
        {
            subtopicNejednacineExisting = new Subtopic("Nejednačine", topicAlgebraExisting.Id);
            db.Set<Subtopic>().Add(subtopicNejednacineExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicTrougloviExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Trouglovi");
        if (subtopicTrougloviExisting == null)
        {
            subtopicTrougloviExisting = new Subtopic("Trouglovi", topicGeometrijaExisting.Id);
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

        var subtopicRazlomciExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Razlomci");
        if (subtopicRazlomciExisting == null)
        {
            subtopicRazlomciExisting = new Subtopic("Razlomci", topicAritmetikaExisting.Id);
            db.Set<Subtopic>().Add(subtopicRazlomciExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicProcentiExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Procenti");
        if (subtopicProcentiExisting == null)
        {
            subtopicProcentiExisting = new Subtopic("Procenti", topicAritmetikaExisting.Id);
            db.Set<Subtopic>().Add(subtopicProcentiExisting);
            await db.SaveChangesAsync();
            changed = true;
        }

        var subtopicKrugExisting = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Krug");
        if (subtopicKrugExisting == null)
        {
            subtopicKrugExisting = new Subtopic("Krug", topicGeometrijaExisting.Id);
            db.Set<Subtopic>().Add(subtopicKrugExisting);
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
            ),
            (
                "Izračunaj: $\\frac{3}{4} + \\frac{1}{8}$",
                2,
                aritmetikaCategory.Id,
                subtopicRazlomciExisting.Id,
                "Prebaci na zajednički imenilac: 3/4 = 6/8, pa 6/8 + 1/8 = 7/8.",
                "\\frac{a}{b}+\\frac{c}{d}=\\frac{ad+bc}{bd}",
                "Nađi zajednički imenilac (8).",
                new() { ("7/8", true), ("1/2", false), ("5/8", false), ("1", false) }
            ),
            (
                "Izračunaj: $2^3 \\cdot 2^2$",
                2,
                algebraCategory.Id,
                subtopicJednacineExisting.Id,
                "Kod istih osnova saberi stepene: 2^{3+2} = 2^5 = 32.",
                "a^m \\cdot a^n = a^{m+n}",
                "Kada je baza ista, sabiraš eksponente.",
                new() { ("32", true), ("16", false), ("64", false), ("10", false) }
            ),
            (
                "Izračunaj: $\\sqrt{49} + \\sqrt{16}$",
                1,
                algebraCategory.Id,
                subtopicJednacineExisting.Id,
                "\\sqrt{49} = 7 i \\sqrt{16} = 4, pa je zbir 11.",
                "\\sqrt{a^2}=a,\\ a\\ge 0",
                "Računaj svaki koren posebno.",
                new() { ("11", true), ("9", false), ("12", false), ("13", false) }
            ),
            (
                "Reši: $|x - 3| = 5$",
                3,
                algebraCategory.Id,
                subtopicJednacineExisting.Id,
                "Dva slučaja: x - 3 = 5 ili x - 3 = -5, pa su rešenja x = 8 ili x = -2.",
                "|u| = k \\Rightarrow u = k \\text{ ili } u = -k",
                "Razdvoji apsolutnu vrednost na dva slučaja.",
                new() { ("x = 8 ili x = -2", true), ("x = 2 ili x = -8", false), ("x = 8", false), ("x = -2", false) }
            ),
            (
                "Reši sistem: $x + y = 7$, $x - y = 1$",
                3,
                algebraCategory.Id,
                subtopicJednacineExisting.Id,
                "Sabiranjem dobijamo 2x = 8, odakle je x = 4. Uvrštavanjem: y = 3.",
                "(x+y) + (x-y) = 7 + 1",
                "Saberi jednačine da eliminišeš y.",
                new() { ("x = 4, y = 3", true), ("x = 3, y = 4", false), ("x = 4, y = 1", false), ("x = 8, y = -1", false) }
            ),
            (
                "Koliko je 15% od 200?",
                1,
                aritmetikaCategory.Id,
                subtopicProcentiExisting.Id,
                "15% od 200 je 0.15 × 200 = 30.",
                "p\\% \\text{ od } N = \\frac{p}{100} \\cdot N",
                "Pretvori procenat u decimalni broj ili razlomak.",
                new() { ("30", true), ("20", false), ("25", false), ("35", false) }
            ),
            (
                "Kolika je površina kruga poluprečnika 3?",
                2,
                geometrijaCategory.Id,
                subtopicKrugExisting.Id,
                "P = \\pi r^2 = \\pi \\cdot 3^2 = 9\\pi.",
                "P = \\pi r^2",
                "Prvo kvadriraj poluprečnik, pa pomnoži sa \\pi.",
                new() { ("9π", true), ("6π", false), ("12π", false), ("18π", false) }
            ),
            (
                "Reši: $\\frac{x}{3} = 4$",
                2,
                algebraCategory.Id,
                subtopicJednacineExisting.Id,
                "Pomnoži obe strane sa 3: x = 12.",
                "\\frac{x}{3}=4 \\Rightarrow x=4\\cdot 3",
                "Oslobodi se imenitelja množenjem sa 3.",
                new() { ("12", true), ("9", false), ("7", false), ("1", false) }
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
            ["Izračunaj: $\\frac{3}{4} + \\frac{1}{8}$"] = new() { ("7/8", true), ("1/2", false), ("5/8", false), ("1", false) },
            ["Izračunaj: $2^3 \\cdot 2^2$"] = new() { ("32", true), ("16", false), ("64", false), ("10", false) },
            ["Izračunaj: $\\sqrt{49} + \\sqrt{16}$"] = new() { ("11", true), ("9", false), ("12", false), ("13", false) },
            ["Reši: $|x - 3| = 5$"] = new() { ("x = 8 ili x = -2", true), ("x = 2 ili x = -8", false), ("x = 8", false), ("x = -2", false) },
            ["Reši sistem: $x + y = 7$, $x - y = 1$"] = new() { ("x = 4, y = 3", true), ("x = 3, y = 4", false), ("x = 4, y = 1", false), ("x = 8, y = -1", false) },
            ["Koliko je 15% od 200?"] = new() { ("30", true), ("20", false), ("25", false), ("35", false) },
            ["Kolika je površina kruga poluprečnika 3?"] = new() { ("9π", true), ("6π", false), ("12π", false), ("18π", false) },
            ["Reši: $\\frac{x}{3} = 4$"] = new() { ("12", true), ("9", false), ("7", false), ("1", false) },
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
            ["Izračunaj: $\\frac{3}{4} + \\frac{1}{8}$"] = ("Compute: $\\frac{3}{4} + \\frac{1}{8}$", "Use a common denominator: 3/4 = 6/8, so 6/8 + 1/8 = 7/8.", "\\frac{a}{b}+\\frac{c}{d}=\\frac{ad+bc}{bd}", "Use 8 as the common denominator."),
            ["Izračunaj: $2^3 \\cdot 2^2$"] = ("Compute: $2^3 \\cdot 2^2$", "With equal bases add exponents: 2^{3+2} = 2^5 = 32.", "a^m \\cdot a^n = a^{m+n}", "Same base means add exponents."),
            ["Izračunaj: $\\sqrt{49} + \\sqrt{16}$"] = ("Compute: $\\sqrt{49} + \\sqrt{16}$", "\\sqrt{49} = 7 and \\sqrt{16} = 4, total is 11.", "\\sqrt{a^2}=a,\\ a\\ge 0", "Evaluate each square root separately."),
            ["Reši: $|x - 3| = 5$"] = ("Solve: $|x - 3| = 5$", "Two cases: x - 3 = 5 or x - 3 = -5, so x = 8 or x = -2.", "|u| = k \\Rightarrow u = k \\text{ or } u = -k", "Split the absolute value into two cases."),
            ["Reši sistem: $x + y = 7$, $x - y = 1$"] = ("Solve the system: $x + y = 7$, $x - y = 1$", "Add equations to get 2x = 8, hence x = 4; then y = 3.", "(x+y)+(x-y)=7+1", "Add the equations to eliminate y."),
            ["Koliko je 15% od 200?"] = ("What is 15% of 200?", "15% of 200 is 0.15 × 200 = 30.", "p\\% \\text{ of } N = \\frac{p}{100} \\cdot N", "Convert percentage to decimal or fraction."),
            ["Kolika je površina kruga poluprečnika 3?"] = ("What is the area of a circle with radius 3?", "A = \\pi r^2 = \\pi · 3^2 = 9\\pi.", "A = \\pi r^2", "Square the radius first, then multiply by \\pi."),
            ["Reši: $\\frac{x}{3} = 4$"] = ("Solve: $\\frac{x}{3} = 4$", "Multiply both sides by 3 to get x = 12.", "\\frac{x}{3}=4 \\Rightarrow x=4\\cdot 3", "Clear the denominator by multiplying by 3."),
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
            ["Izračunaj: $\\frac{3}{4} + \\frac{1}{8}$"] = new()
            {
                ("Nađi zajednički imenilac: 3/4 pretvori u 6/8", "Množi brojilac i imenilac sa 2", false, "Find a common denominator: convert 3/4 to 6/8", "Multiply numerator and denominator by 2"),
                ("Saberi razlomke: 6/8 + 1/8 = 7/8", null, false, "Add fractions: 6/8 + 1/8 = 7/8", null),
                ("Konačno rešenje je 7/8", null, true, "Final answer is 7/8", null),
            },
            ["Izračunaj: $2^3 \\cdot 2^2$"] = new()
            {
                ("Primeni pravilo: 2^3 × 2^2 = 2^(3+2)", "Kod istih osnova sabiraju se stepeni", false, "Apply the rule: 2^3 × 2^2 = 2^(3+2)", "With equal bases, add exponents"),
                ("Dobijamo 2^5", null, false, "We get 2^5", null),
                ("2^5 = 32", null, true, "2^5 = 32", null),
            },
            ["Izračunaj: $\\sqrt{49} + \\sqrt{16}$"] = new()
            {
                ("Izračunaj korene: √49 = 7 i √16 = 4", null, false, "Evaluate roots: √49 = 7 and √16 = 4", null),
                ("Saberi rezultate: 7 + 4", null, false, "Add the results: 7 + 4", null),
                ("Konačno: 11", null, true, "Final: 11", null),
            },
            ["Reši: $|x - 3| = 5$"] = new()
            {
                ("Apsolutna vrednost daje dva slučaja: x - 3 = 5 ili x - 3 = -5", "|u| = k znači u = k ili u = -k", false, "Absolute value gives two cases: x - 3 = 5 or x - 3 = -5", "|u| = k means u = k or u = -k"),
                ("Prvi slučaj: x - 3 = 5 ⇒ x = 8", null, false, "First case: x - 3 = 5 ⇒ x = 8", null),
                ("Drugi slučaj: x - 3 = -5 ⇒ x = -2", null, false, "Second case: x - 3 = -5 ⇒ x = -2", null),
                ("Rešenja su x = 8 ili x = -2", null, true, "Solutions are x = 8 or x = -2", null),
            },
            ["Reši sistem: $x + y = 7$, $x - y = 1$"] = new()
            {
                ("Saberi jednačine: (x + y) + (x - y) = 7 + 1", "y se poništava", false, "Add equations: (x + y) + (x - y) = 7 + 1", "y gets eliminated"),
                ("Dobijamo 2x = 8, pa je x = 4", null, false, "We get 2x = 8, so x = 4", null),
                ("Uvrsti u prvu jednačinu: 4 + y = 7", null, false, "Substitute into first equation: 4 + y = 7", null),
                ("Dobijamo y = 3, rešenje je (x, y) = (4, 3)", null, true, "We get y = 3, solution is (x, y) = (4, 3)", null),
            },
            ["Koliko je 15% od 200?"] = new()
            {
                ("Pretvori procenat: 15% = 15/100 = 0.15", null, false, "Convert percentage: 15% = 15/100 = 0.15", null),
                ("Izračunaj: 0.15 × 200", null, false, "Compute: 0.15 × 200", null),
                ("Konačno: 30", null, true, "Final: 30", null),
            },
            ["Kolika je površina kruga poluprečnika 3?"] = new()
            {
                ("Formula za površinu kruga: P = πr²", null, false, "Circle area formula: A = πr²", null),
                ("Uvrsti r = 3: P = π × 3² = 9π", null, false, "Substitute r = 3: A = π × 3² = 9π", null),
                ("Konačan rezultat: 9π", null, true, "Final result: 9π", null),
            },
            ["Reši: $\\frac{x}{3} = 4$"] = new()
            {
                ("Početno: x/3 = 4", null, false, "Start: x/3 = 4", null),
                ("Pomnoži obe strane sa 3", "Uklanjaš imenilac 3", false, "Multiply both sides by 3", "This clears denominator 3"),
                ("Dobijamo x = 12", null, true, "We get x = 12", null),
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

        // ── Explanation engine reference data (idempotent) ──
        var formulaSeeds = new[]
        {
            new MathFormulaReferenceEntity(
                "quadratic_formula",
                "Quadratic Formula",
                "x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>x</mi><mo>=</mo><mfrac><mrow><mo>-</mo><mi>b</mi><mo>&#x00B1;</mo><msqrt><msup><mi>b</mi><mn>2</mn></msup><mo>-</mo><mn>4</mn><mi>a</mi><mi>c</mi></msqrt></mrow><mrow><mn>2</mn><mi>a</mi></mrow></mfrac></mrow></math>",
                "Used to solve quadratic equations of the form ax^2 + bx + c = 0."),
            new MathFormulaReferenceEntity(
                "fraction_simplification_rule",
                "Fraction Simplification Rule",
                "\\frac{a}{b} = \\frac{a \\div d}{b \\div d}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mfrac><mi>a</mi><mi>b</mi></mfrac><mo>=</mo><mfrac><mrow><mi>a</mi><mo>&#x00F7;</mo><mi>d</mi></mrow><mrow><mi>b</mi><mo>&#x00F7;</mo><mi>d</mi></mrow></mfrac></mrow></math>",
                "Divide numerator and denominator by the same common divisor."),
            new MathFormulaReferenceEntity(
                "fraction_addition_rule",
                "Fraction Addition Rule",
                "\\frac{a}{d} + \\frac{b}{d} = \\frac{a+b}{d}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mfrac><mi>a</mi><mi>d</mi></mfrac><mo>+</mo><mfrac><mi>b</mi><mi>d</mi></mfrac><mo>=</mo><mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mi>d</mi></mfrac></mrow></math>",
                "When denominators are equal, add only the numerators."),
            new MathFormulaReferenceEntity(
                "linear_equation_isolation",
                "Linear Equation Isolation",
                "ax + b = c \\Rightarrow x = \\frac{c-b}{a}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>a</mi><mi>x</mi><mo>+</mo><mi>b</mi><mo>=</mo><mi>c</mi><mo>&#x21D2;</mo><mi>x</mi><mo>=</mo><mfrac><mrow><mi>c</mi><mo>-</mo><mi>b</mi></mrow><mi>a</mi></mfrac></mrow></math>",
                "Move the constant term first, then divide by the coefficient."),
            new MathFormulaReferenceEntity(
                "area_of_triangle",
                "Area of Triangle",
                "A = \\frac{b \\cdot h}{2}",
                "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>A</mi><mo>=</mo><mfrac><mrow><mi>b</mi><mo>&#x22C5;</mo><mi>h</mi></mrow><mn>2</mn></mfrac></mrow></math>",
                "Triangle area equals base times height divided by two.")
        };

        var existingFormulaIds = (await db.Set<MathFormulaReferenceEntity>()
            .Select(x => x.Id)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var formulasToInsert = formulaSeeds
            .Where(x => !existingFormulaIds.Contains(x.Id))
            .ToList();
        if (formulasToInsert.Count > 0)
        {
            db.Set<MathFormulaReferenceEntity>().AddRange(formulasToInsert);
            await db.SaveChangesAsync();
            changed = true;
        }

        var ruleSeeds = new[]
        {
            new MathTransformationRule("ADD_FRACTIONS", "Add Fractions", "Find a common denominator and combine numerators.", "TRANSFORMATION", @"^\s*-?\d+/\d+\s*[+-]\s*-?\d+/\d+\s*$", "\\frac{a}{d}+\\frac{b}{d}=\\frac{a+b}{d}"),
            new MathTransformationRule("SIMPLIFY_FRACTION", "Simplify Fraction", "Reduce a fraction using a common divisor.", "SIMPLIFICATION", @"^\s*-?\d+/\d+\s*$", "\\frac{a}{b}=\\frac{a\\div d}{b\\div d}"),
            new MathTransformationRule("ISOLATE_VARIABLE", "Isolate Variable", "Undo operations in reverse order to isolate the variable.", "TRANSFORMATION", @"x", "ax+b=c \\Rightarrow x=\\frac{c-b}{a}"),
            new MathTransformationRule("APPLY_FORMULA", "Apply Formula", "Substitute known values into a standard formula.", "FORMULA", null, "x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}"),
            new MathTransformationRule("EVALUATE_ARITHMETIC", "Evaluate Arithmetic", "Compute the arithmetic operation directly.", "CALCULATION", @"^\s*-?\d+\s*[+\-*/×]\s*-?\d+\s*$", "a+b")
        };

        var existingRuleIds = (await db.Set<MathTransformationRule>()
            .Select(x => x.Id)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rulesToInsert = ruleSeeds
            .Where(x => !existingRuleIds.Contains(x.Id))
            .ToList();
        if (rulesToInsert.Count > 0)
        {
            db.Set<MathTransformationRule>().AddRange(rulesToInsert);
            await db.SaveChangesAsync();
            changed = true;
        }

        var existingPatternKeys = (await db.Set<CommonMistakePattern>()
            .Select(x => x.PatternKey)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mistakeSeeds = new[]
        {
            new CommonMistakePattern("Fractions", "Addition", "FRACTION_DENOMINATOR_ADDITION", "fractions_add_same_denominator_wrong_sum", "You added the denominators together. The denominator stays fixed when fractions already share the same whole.", "Keep the denominator and add only the numerators.", 10),
            new CommonMistakePattern("Algebra", "Linear equations", "SIGN_ERROR", "linear_equation_sign_flip", "The constant term was moved with the wrong sign.", "Use the opposite operation on both sides before dividing.", 20),
            new CommonMistakePattern("Algebra", "Quadratic equations", "INCORRECT_FORMULA_USAGE", "quadratic_formula_denominator", "The quadratic formula denominator must be 2a.", "Write the full formula first, then substitute values.", 30)
        };
        var mistakesToInsert = mistakeSeeds
            .Where(x => !existingPatternKeys.Contains(x.PatternKey))
            .ToList();
        if (mistakesToInsert.Count > 0)
        {
            db.Set<CommonMistakePattern>().AddRange(mistakesToInsert);
            await db.SaveChangesAsync();
            changed = true;
        }

        // UserProfiles are 1:1 with Identity users (AspNetUsers) and should be created via UserManager
        // (e.g. during app startup backfill), not by this data seeder.

        if (changed)
        {
            Console.WriteLine("✓ Database seeded with initial data");
        }
    }
}
