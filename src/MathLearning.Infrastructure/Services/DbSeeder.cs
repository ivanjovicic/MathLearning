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
