# MathLearning Translation Job

This console application automatically generates translations for questions and options using external translation APIs.

## Supported Providers

- **Google Translate**: `google` (requires Google Cloud API key)
- **DeepL**: `deepl` (requires DeepL API key)

## Setup

1. Get an API key from your chosen provider:
   - [Google Cloud Translation API](https://cloud.google.com/translate)
   - [DeepL API](https://www.deepl.com/pro-api)

2. Set the API key as an environment variable:
   ```bash
   export TRANSLATION_API_KEY=your_api_key_here
   ```
   Or pass it as the third argument.

## Usage

### Basic Usage
```bash
# Using Google Translate
dotnet run --project src/MathLearning.TranslationJob -- sr google YOUR_API_KEY

# Using DeepL
dotnet run --project src/MathLearning.TranslationJob -- de deepl YOUR_API_KEY
```

### Batch Script
```bash
# Windows
scripts\run-translation-job.bat sr google YOUR_API_KEY

# Or with environment variable
set TRANSLATION_API_KEY=YOUR_API_KEY
scripts\run-translation-job.bat sr google
```

## What It Does

1. **Finds missing translations**: Scans the database for questions/options that don't have translations in the target language
2. **Translates content**: Uses the specified API to translate:
   - Question text
   - Question explanations
   - Hint formulas
   - Hint clues
   - **Hint light** (new)
   - **Hint medium** (new)
   - **Hint full** (new)
   - Option texts
3. **Fills missing hint fields**: For existing translations, populates any missing hint fields (HintLight, HintMedium, HintFull)
4. **Saves translations**: Creates `QuestionTranslation` and `OptionTranslation` records
5. **Rate limiting**: Includes delays to avoid hitting API limits

## Database Requirements

- PostgreSQL database must be running (see main setup instructions)
- Existing questions with English translations or text
- EF Core migrations must be applied

## Example Output

```
Starting translation job: sr using google
Found 8 questions without 'sr' translation.
✓ Translated Q=1 → sr
✓ Translated Q=2 → sr
...
Completed: 8 translated, 0 errors
```

## API Limits

- **Google Translate**: 2 million characters/month free, then $20 per 1M characters
- **DeepL**: 500,000 characters/month free, then pay-as-you-go

The job includes rate limiting (100ms between requests) to stay within reasonable limits.

## Troubleshooting

- **"API key not provided"**: Set `TRANSLATION_API_KEY` environment variable or pass as argument
- **"Unknown provider"**: Use `google` or `deepl`
- **Database connection errors**: Make sure PostgreSQL is running on localhost:5433
- **Rate limiting**: If you hit API limits, wait and try again, or reduce batch size