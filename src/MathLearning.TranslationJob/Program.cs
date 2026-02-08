using MathLearning.TranslationJob.Services;
using MathLearning.Infrastructure.Persistance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MathLearning.TranslationJob;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: MathLearning.TranslationJob <target-lang> <provider> [api-key]");
            Console.WriteLine("Examples:");
            Console.WriteLine("  MathLearning.TranslationJob sr google YOUR_GOOGLE_API_KEY");
            Console.WriteLine("  MathLearning.TranslationJob de deepl YOUR_DEEPL_API_KEY");
            return;
        }

        var targetLang = args[0];
        var provider = args[1].ToLower();
        var apiKey = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("TRANSLATION_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key not provided via args or TRANSLATION_API_KEY env var");
            return;
        }

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Database
                services.AddDbContext<ApiDbContext>(options =>
                    options.UseNpgsql("Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=mathlearning;"));

                // HTTP client for translation APIs
                services.AddHttpClient();

                // Translation client
                if (provider == "google")
                {
                    services.AddSingleton<ITranslationClient>(sp =>
                        new GoogleTranslateClient(sp.GetRequiredService<HttpClient>(), apiKey));
                }
                else if (provider == "deepl")
                {
                    services.AddSingleton<ITranslationClient>(sp =>
                        new DeepLTranslateClient(sp.GetRequiredService<HttpClient>(), apiKey));
                }
                else
                {
                    throw new ArgumentException($"Unknown provider: {provider}. Use 'google' or 'deepl'");
                }

                // Translation job
                services.AddScoped<QuestionTranslationJob>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var job = scope.ServiceProvider.GetRequiredService<QuestionTranslationJob>();

        try
        {
            Console.WriteLine($"Starting translation job: {targetLang} using {provider}");

            // Apply any pending migrations
            await db.Database.MigrateAsync();

            // Generate translations
            await job.GenerateMissingTranslationsAsync(targetLang);
            await job.GenerateOptionTranslationsAsync(targetLang);

            // Fill missing hint fields in existing translations
            await job.FillMissingHintFieldsAsync(targetLang);

            Console.WriteLine("Translation job completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
