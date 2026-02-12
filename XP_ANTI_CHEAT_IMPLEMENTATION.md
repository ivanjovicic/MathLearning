# XP & Anti-Cheat System Implementation

## 📊 Overview
Implementiran je sistem dodeljavanja XP-a koji sprečava varanje i prati sve odgovore korisnika.

## ✅ Implementirano

### 1. **XP po težini pitanja**
```csharp
Difficulty 1 → 5 XP   (Easy)
Difficulty 2 → 10 XP  (Medium)
Difficulty 3 → 15 XP  (Hard)
Difficulty 4 → 20 XP  (Very Hard)
Difficulty 5 → 25 XP  (Expert)
```

### 2. **Anti-cheat logika**
- XP se dodeljuje **samo prvi put** kada korisnik tačno reši pitanje
- `UserQuestionStat.CorrectAttempts == 0` → dodeli XP
- Svaki sledeći tačan odgovor → 0 XP (već nagrađeno)

### 3. **Audit logging**
Nova tabela: `UserAnswerAudits`
- `UserId` — ko je odgovorio
- `QuestionId` — koje pitanje
- `Answer` — šta je odgovoreno
- `IsCorrect` — tačno/netačno
- `AwardedXp` — koliko XP je dodeljeno
- `AnsweredAt` — kada

**Koristi se za:**
- Debugging (zašto je neko dobio/nije dobio XP)
- Anti-cheat analiza (detektovanje šablona, spamovanja)
- Istorija pokušaja

### 4. **SubmitAnswerResponse proširen**
```csharp
public record SubmitAnswerResponse(
    bool IsCorrect,
    string? Explanation,
    List<StepExplanationDto>? Steps = null,
    bool IsFirstTimeCorrect = false,   // ✅ NOVO
    int AwardedXp = 0,                  // ✅ NOVO
    int TotalXp = 0                     // ✅ NOVO
);
```

## 🗂️ Dodati fajlovi
1. `src/MathLearning.Domain/Entities/UserAnswerAudit.cs` — entity za audit log
2. `src/MathLearning.Infrastructure/Migrations/Api/20260211120000_AddUserAnswerAudit.cs` — migration

## 🔧 Izmenjeni fajlovi
1. `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
   - Dodat `DbSet<UserAnswerAudit>`

2. `src/MathLearning.Application/DTOs/Quiz/SubmitAnswerResponse.cs`
   - Dodati novi parametri: `IsFirstTimeCorrect`, `AwardedXp`, `TotalXp`

3. `src/MathLearning.Api/Endpoints/QuizEndpoints.cs` (submit answer endpoint)
   - Dodato: XP kalkulacija po težini
   - Dodato: Provera da li je prvi tačan odgovor
   - Dodato: Kreiranje/ažuriranje `UserProfile.Xp`
   - Dodato: Dodavanje zapisa u `UserAnswerAudits`
   - Vraća: Prošireni response sa XP podacima

## 🚀 Sledeći koraci (opciono)
1. **Race condition handling**: Dodati `try/catch` sa `DbUpdateException` i reload profila
2. **Dashboard za anti-cheat**: Admin panel koji prikazuje sumnjive odgovore
3. **XP leaderboard**: Rangiranje korisnika po XP-u
4. **Achievements/Badges**: Nagrade za određene milestones (npr. "100 XP earned")

## 📝 Kako pokrenuti
```bash
# Primeni migration na bazu
dotnet ef database update --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api

# Build i pokreni
dotnet build
dotnet run --project src/MathLearning.Api
```

## 🧪 Testiranje
Pozovi `/api/quiz/answer` endpoint i proveri response:
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

## 🔒 Sigurnost
- ✅ Server-side validacija odgovora (ne verujemo klijentu)
- ✅ XP se dodeljuje samo jednom po pitanju
- ✅ Audit log za sve odgovore (sprečava manipulaciju istorije)
- ✅ Unique index na `UserAnswers(UserId, QuestionId, AnsweredAt)` — sprečava duplikate
