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
                new Subtopic("Sabiranje do 100", t3.Id)
            );
            await db.SaveChangesAsync();
            changed = true;
        }

        // ── Questions ────────────────────────────────
        if (!await db.Set<Question>().AnyAsync())
        {
            var algebra = await db.Set<Category>().FirstAsync(c => c.Name == "Algebra");
            var aritmetika = await db.Set<Category>().FirstAsync(c => c.Name == "Aritmetika");
            var subtopicJednacine = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Jednačine");
            var subtopicSabiranje = await db.Set<Subtopic>().FirstAsync(s => s.Name == "Sabiranje do 100");

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
        }

        // ── LaTeX test questions (idempotent) ─────────
        var algebraCategory = await db.Set<Category>().FirstOrDefaultAsync(c => c.Name == "Algebra");
        var equationsSubtopic = await db.Set<Subtopic>().FirstOrDefaultAsync(s => s.Name == "Jednačine");

        if (algebraCategory != null && equationsSubtopic != null)
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

                var q = new Question(seed.Text, seed.Difficulty, algebraCategory.Id, seed.Explanation);
                q.SetSubtopic(equationsSubtopic.Id);
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
