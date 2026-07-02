# Rate Limiting Implementation Guide

## ?? Overview
Implementiran je rate limiting sistem koji sprecava spam pokušaje odgovaranja na ista pitanja.

## ? Šta je dodato

### 1. **UserQuestionAttempt Entity**
```csharp
public class UserQuestionAttempt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuestionId { get; set; }
    public DateTime AttemptedAt { get; set; }
}
```

**Svrha**: Beleži svaki pokušaj odgovora na pitanje (ne ceka se validacija, cim zahtev stigne).

### 2. **Rate Limiting Logika**
- **Limit**: Max **10 pokušaja po pitanju u 1 minutu**
- **Response**: HTTP 429 (Too Many Requests)
- **Poruka**: "Too many attempts. Please slow down."

### 3. **Performance Indexes**
```sql
-- Kompozitni index za brzo pretraživanje
IX_UserQuestionAttempts_UserId_QuestionId_AttemptedAt

-- Index za cleanup operacije
IX_UserQuestionAttempts_AttemptedAt
```

## ?? Implementacija u Submit Answer Endpoint

### Pre validacije odgovora:
```csharp
// ?? RATE LIMITING - Max 10 attempts per question per minute
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

## ??? Database Schema

### Table: UserQuestionAttempts
| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key |
| UserId | int | User koji pokušava |
| QuestionId | int | Pitanje na koje se odgovara |
| AttemptedAt | timestamp | Vreme pokušaja (UTC) |

## ?? Kako radi

### Scenario 1: Normalno korišcenje
```
User odgovara na pitanje ID=5
-> Count attempts za User/Question u poslednjih 60s
-> 3 attempts -> OK, allow
-> Dodaj novi attempt zapis
-> Process odgovor
```

### Scenario 2: Spam detektovan
```
User šalje 11. request za pitanje ID=5 u roku od 1 minute
-> Count attempts = 10
-> REJECT sa 429 Too Many Requests
-> Ne procesira odgovor, ne dodaje attempt zapis
```

## ?? Cleanup Strategija

### Opcija 1: Background Job (preporuceno za production)
```csharp
// U Program.cs dodaj:
builder.Services.AddHostedService<AttemptCleanupService>();

// Cleanup service briše zapise starije od 24h svaki sat
```

### Opcija 2: Manual cleanup (za development)
```sql
-- Briši pokušaje starije od 24h
DELETE FROM "UserQuestionAttempts"
WHERE "AttemptedAt" < NOW() - INTERVAL '24 hours';
```

### Opcija 3: PostgreSQL retention policy (najbolje)
```sql
-- Periodic VACUUM + partition by date
-- Konfiguracija za auto-cleanup starih particija
```

## ?? Deployment Steps

1. **Primeni migration**:
```bash
dotnet ef database update --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api
```

2. **Testiraj rate limiting**:
```bash
# Pošalji 11 zahteva brzo za isto pitanje
for i in {1..11}; do
  curl -X POST https://localhost:5001/api/quiz/answer \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"quizId":"...","questionId":5,"answer":"A","timeSpentSeconds":1}'
done
```

3. **Proveri logs**:
Ocekivano: prvih 10 zahteva prolaze, 11. vraca 429.

## ?? Configuration Options (opciono)

Možeš dodati konfiguraciju umesto hardcoded vrednosti:

```csharp
// appsettings.json
{
  "RateLimiting": {
    "MaxAttemptsPerMinute": 10,
    "WindowSizeMinutes": 1
  }
}

// U endpoint:
var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
int maxAttempts = config.GetValue<int>("RateLimiting:MaxAttemptsPerMinute");
```

## ?? Sigurnost

### Prednosti:
? Sprecava brute-force attacking (nasumicno probavanje odgovora)  
? Štedi server resurse (CPU, DB connections)  
? Cini botove/skripte teže izvodljivim  
? Ne utice na normalne korisnike (10 pokušaja u 1 min je dovoljno)

### Dodatni slojevi (opciono):
- **Global rate limit** - Max requests per user per minute (svi endpointi)
- **IP-based rate limit** - Limit po IP adresi (pre autentifikacije)
- **CAPTCHA** - Ako se detektuje spam, zatraži CAPTCHA

## ?? Testing Scenarios

### Test 1: Normal usage
```
? 5 attempts in 2 minutes ? All pass
```

### Test 2: Rapid attempts
```
? 11 attempts in 30 seconds ? First 10 pass, 11th returns 429
```

### Test 3: Different questions
```
? 10 attempts on Q1, 10 attempts on Q2 ? All pass (different questions)
```

### Test 4: Window expiry
```
? 10 attempts, wait 61 seconds, 10 more attempts ? All pass
```

## ?? Troubleshooting

### Problem: Rate limit hit prematurely
**Uzrok**: Clock skew ili stari zapisi nisu obrisani  
**Rešenje**: Proveri `AttemptedAt` u bazi, osiguraj da je cleanup aktivan

### Problem: Performance degradacija
**Uzrok**: Previše zapisa u tabeli  
**Rešenje**: Pokreni cleanup, dodaj composite index

### Problem: False positives
**Uzrok**: Više korisnika na istoj mašini (shared account)  
**Rešenje**: Uvedi IP-based tracking ili relaksiraj limit

## ?? Metrics to Track

- **Average attempts per question** (da bi se podesio optimalni limit)
- **429 response rate** (koliko cesto ljudi pogadaju limit)
- **Table growth rate** (da bi se odredio cleanup interval)

---

**Status**: ? Implementirano  
**Build**: ? Successful  
**Migration**: ?? Ready to apply
