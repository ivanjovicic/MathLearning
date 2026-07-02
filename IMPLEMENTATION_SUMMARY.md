# Summary - XP, Anti-Cheat & Rate Limiting Implementation

## ✅ Šta je implementirano

### 1. **XP System sa Anti-Cheat**
- ✅ `UserAnswerAudit` entity - audit log za sve odgovore
- ✅ `SubmitAnswerResponse` proširen - dodati `IsFirstTimeCorrect`, `AwardedXp`, `TotalXp`
- ✅ XP po težini: 1→5XP, 2→10XP, 3→15XP, 4→20XP, 5→25XP
- ✅ Anti-cheat: XP se dodeljuje samo prvi put kada korisnik tačno reši
- ✅ Audit logging: Svi odgovori se loguju u `UserAnswerAudits` tabelu

### 2. **Rate Limiting**
- ✅ `UserQuestionAttempt` entity - prati sve pokušaje
- ✅ Limit: Max 10 pokušaja po pitanju u 1 minutu
- ✅ Response: HTTP 429 (Too Many Requests)
- ✅ Performance indexes za brze upite

## 📁 Dodati fajlovi

1. **Entities**:
   - `src/MathLearning.Domain/Entities/UserAnswerAudit.cs`
   - `src/MathLearning.Domain/Entities/UserQuestionAttempt.cs`

2. **Migrations**:
   - `src/MathLearning.Infrastructure/Migrations/Api/20260211120000_AddUserAnswerAudit.cs`
   - `src/MathLearning.Infrastructure/Migrations/Api/20260211121500_AddUserQuestionAttemptRateLimiting.cs`

3. **Documentation**:
   - `XP_ANTI_CHEAT_IMPLEMENTATION.md`
   - `RATE_LIMITING_GUIDE.md`

## 🔧 Izmenjeni fajlovi

1. **`ApiDbContext.cs`**:
   - Dodati DbSet-ovi: `UserAnswerAudits`, `UserQuestionAttempts`

2. **`SubmitAnswerResponse.cs`**:
   - Prošireni parametri za XP tracking

3. **`QuizEndpoints.cs`** (manual update needed):
   - **Trenutno**: Još nije apliciran rate limiting i XP kod u submit answer endpoint
   - **Potrebno**: Dodati rate limiting check i XP calculation logiku

## ⚠️ Preostalo za ručno dodavanje

Otvori `src/MathLearning.Api/Endpoints/QuizEndpoints.cs` i u submit answer endpoint (`group.MapPost("/answer", ...)`) dodaj:

### 1. Nakon `if (question == null) return Results.NotFound(...);` dodaj:

```csharp
// 🚦 RATE LIMITING - Max 10 attempts per question per minute
var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
var recentAttemptsCount = await db.UserQuestionAttempts
    .CountAsync(a =>
        a.UserId == userId &&
        a.QuestionId == question.Id &&
        a.AttemptedAt >= oneMinuteAgo);

if (recentAttemptsCount >= 10)
{
    return Results.Json(
        new { error = "Too many attempts. Please slow down." },
        statusCode: 429
    );
}

// Record attempt
db.UserQuestionAttempts.Add(new UserQuestionAttempt
{
    UserId = userId,
    QuestionId = question.Id,
    AttemptedAt = DateTime.UtcNow
});
```

### 2. Zameni liniju `stat.Attempts++;` i sve do `await db.SaveChangesAsync();` sa:

```csharp
stat.Attempts++;

// XP by difficulty mapping
int CalculateXp(int difficulty) => difficulty switch
{
    1 => 5,   // Easy
    2 => 10,  // Medium
    3 => 15,  // Hard
    4 => 20,  // Very Hard
    5 => 25,  // Expert
    _ => 10
};

var awardedXp = 0;
var isFirstTimeCorrect = false;

if (isCorrect)
{
    if (stat.CorrectAttempts == 0)
    {
        isFirstTimeCorrect = true;
        awardedXp = CalculateXp(question.Difficulty);

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId, Xp = 0 };
            db.UserProfiles.Add(profile);
        }

        profile.Xp += awardedXp;
    }

    stat.CorrectAttempts++;
}

stat.LastAttemptAt = DateTime.UtcNow;

// Audit log
db.UserAnswerAudits.Add(new UserAnswerAudit
{
    UserId = userId,
    QuestionId = question.Id,
    Answer = request.Answer,
    IsCorrect = isCorrect,
    AwardedXp = awardedXp,
    AnsweredAt = DateTime.UtcNow
});

await db.SaveChangesAsync();
```

### 3. Zameni `return Results.Ok(new SubmitAnswerResponse(...));` sa:

```csharp
// Return steps only on incorrect answer
var steps = isCorrect ? null : StepEngine.GetSteps(question, lang);

var currentXp = (await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId))?.Xp ?? 0;

return Results.Ok(new SubmitAnswerResponse(
    isCorrect,
    isCorrect ? null : TranslationHelper.GetExplanation(question, lang),
    steps,
    isFirstTimeCorrect,
    awardedXp,
    currentXp
));
```

## 🚀 Posle ručnih izmena:

1. **Build projekat**:
```bash
dotnet build
```

2. **Primeni migrations**:
```bash
dotnet ef database update --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api
```

3. **Testiraj**:
   - Submit answer → proveri da vraća XP podatke
   - Submit 11 requests brzo → 11. treba da vrati 429

## 📊 Finalni rezultat

### Submit Answer Response:
```json
{
  "isCorrect": true,
  "explanation": null,
  "steps": null,
  "isFirstTimeCorrect": true,
  "awardedXp": 15,
  "totalXp": 135
}
```

### Rate Limit Response:
```json
{
  "error": "Too many attempts. Please slow down."
}
```

---

**Build Status**: ✅ Successful (entities & migrations)  
**Manual Code Changes**: ⏳ Pending (QuizEndpoints.cs)  
**Database Migration**: 📋 Ready to apply
