# Database Transactions in Offline Batch Submit

## 🔄 Zašto Transakcije?

### Problem bez Transakcije

**Scenario**: Korisnik šalje batch sa 50 odgovora. Nakon što se upiše 30 odgovora, desi se greška (npr. database timeout, constraint violation).

**❌ Bez transakcije**:
- 30 odgovora je već upisano u bazu
- 20 odgovora nije upisano
- Statistike su parcijalno ažurirane
- **Rezultat**: Nekonzistentno stanje baze podataka!

**✅ Sa transakcijom**:
- Svi upisi se čuvaju u memoriji dok se ne pozove `CommitAsync()`
- Ako dođe do greške, `RollbackAsync()` vraća sve na staro
- **Rezultat**: Ili svi odgovori ili nijedan - atomičnost garantovana!

## 🎯 ACID Osobine

### Atomicity (Atomičnost)
```csharp
await using var trx = await db.Database.BeginTransactionAsync();
try {
    // Sve operacije su jedna celina
    await db.SaveChangesAsync();
    await trx.CommitAsync(); // Sve ili ništa
}
catch {
    await trx.RollbackAsync(); // Vrati sve na staro
}
```

**Benefit**: Batch operacija je "all-or-nothing" - nema parcijalnih upisa.

### Consistency (Konzistentnost)
```csharp
// Pre transakcije
UserAnswers: 100 records
UserQuestionStats.Attempts: 500

// Posle neuspešnog batch-a
UserAnswers: 100 records (isto!)
UserQuestionStats.Attempts: 500 (isto!)
```

**Benefit**: Baza ostaje u validnom stanju čak i pri greškama.

### Isolation (Izolacija)
```csharp
// User A pokreće batch import
await using var trx = await db.Database.BeginTransactionAsync();

// User B ne vidi promene dok se ne commit-uje
var stats = await db.UserQuestionStats.ToListAsync(); // Stare vrednosti

// Tek nakon commit-a User B vidi nove podatke
await trx.CommitAsync();
```

**Benefit**: Drugi zahtevi ne vide "dirty reads" - nekompletne podatke.

### Durability (Trajnost)
```csharp
await trx.CommitAsync();
// Nakon commit-a, podaci su trajno sačuvani
// Čak i ako server padne posle commit-a, podaci su safe
```

**Benefit**: Garantuje se da su podaci trajno sačuvani u bazi.

## 📊 Implementacija

### Pre Transakcije
```csharp
group.MapPost("/offline-submit", async (request, db, ctx) =>
{
    // ...
    
    foreach (var answer in request.Answers)
    {
        db.UserAnswers.Add(new UserAnswer { ... });
        // ⚠️ Ako ovde pukne, prethodni upisi su već u bazi!
    }
    
    await db.SaveChangesAsync();
    
    return Results.Ok(...);
});
```

**Problem**: Ako `SaveChangesAsync()` ili bilo koja operacija fail-uje, imaš parcijalne podatke u bazi.

### Posle Transakcije
```csharp
group.MapPost("/offline-submit", async (request, db, ctx) =>
{
    await using var trx = await db.Database.BeginTransactionAsync();
    
    try
    {
        foreach (var answer in request.Answers)
        {
            db.UserAnswers.Add(new UserAnswer { ... });
            // ✅ Sve je u memoriji, ništa nije upisano u bazu još
        }
        
        await db.SaveChangesAsync(); // Pripremi upis
        await trx.CommitAsync();     // Atomički upiši sve odjednom
        
        return Results.Ok(...);
    }
    catch (Exception ex)
    {
        await trx.RollbackAsync(); // Odbaci sve promene
        return Results.Problem(...);
    }
});
```

**Benefit**: Garantovana atomičnost - ili sve ili ništa.

## 🔍 Real-World Scenarios

### Scenario 1: Database Timeout
```csharp
// Korisnik šalje 100 odgovora
// Posle 50 upisa, database timeout (npr. slow network)

// ❌ Bez transakcije:
UserAnswers: +50 (parcijalno upisano)
UserQuestionStats: delimično ažurirano
// ❗ Nekonzistentno stanje!

// ✅ Sa transakcijom:
await trx.RollbackAsync();
UserAnswers: +0 (ništa nije upisano)
UserQuestionStats: nepromenjeno
// ✅ Konzistentno stanje!
```

### Scenario 2: Constraint Violation
```csharp
// Duplikat answer zbog race condition
// EF Core baca DbUpdateException

// ❌ Bez transakcije:
// 30 odgovora već upisano, 31. fail-uje
// Ostali odgovori se nikad ne upišu

// ✅ Sa transakcijom:
await trx.RollbackAsync();
// Svi odgovori se odbacuju
// Korisnik može retry-ovati ceo batch
```

### Scenario 3: Application Crash
```csharp
// Posle 40 upisa, server se restartuje

// ❌ Bez transakcije:
// 40 odgovora je upisano
// Korisnik ne zna koji su upisani a koji nisu
// Mora ručno da proverava i retry-uje

// ✅ Sa transakcijom:
// Transakcija se automatski rollback-uje pri crash-u
// Ništa nije upisano
// Korisnik retry-uje ceo batch sa idempotency check-om
```

## ⚡ Performance Considerations

### Je Li Transakcija Spora?

**Myth**: "Transakcije usporavaju aplikaciju"

**Reality**: 
- **Neznatno spor** - overhead je ~5-10ms za transakciju
- **Brže od multiple savechanges** - jedna transakcija je brža od N pojedinačnih upisa

### Benchmark
```csharp
// ❌ Bez transakcije (multiple SaveChanges)
foreach (var answer in answers) {
    db.UserAnswers.Add(answer);
    await db.SaveChangesAsync(); // N round-trips
}
// Vreme: ~500ms za 50 odgovora

// ✅ Sa transakcijom (batch SaveChanges)
await using var trx = await db.Database.BeginTransactionAsync();
foreach (var answer in answers) {
    db.UserAnswers.Add(answer);
}
await db.SaveChangesAsync(); // 1 round-trip
await trx.CommitAsync();
// Vreme: ~100ms za 50 odgovora
```

**Benefit**: Transakcija je **brža** jer sve upise komituje odjednom!

## 🔐 Transaction Isolation Levels

### Default (Read Committed)
```csharp
// Default isolation level u većini baza
await using var trx = await db.Database.BeginTransactionAsync();
```

**Characteristics**:
- Sprečava "dirty reads"
- Dozvoljava "non-repeatable reads"
- Najbolji balans performance/consistency

### Serializable (Strict)
```csharp
await using var trx = await db.Database.BeginTransactionAsync(
    System.Data.IsolationLevel.Serializable);
```

**Characteristics**:
- Najstroži nivo
- Sprečava sve anomalije
- **Sporiji** - lock-uje sve resource-e

**Use Case**: Finansijske transakcije, kritične operacije

### Read Uncommitted (Risky)
```csharp
await using var trx = await db.Database.BeginTransactionAsync(
    System.Data.IsolationLevel.ReadUncommitted);
```

**Characteristics**:
- Dozvoljava "dirty reads"
- **Najbrži** - bez lock-ova
- **Rizičan** - može videti nevalidne podatke

**Use Case**: Read-only reports, analytics

### Recommended for Offline Batch
```csharp
// Default (Read Committed) je dovoljan
await using var trx = await db.Database.BeginTransactionAsync();
```

## 🧪 Testing Transactions

### Test 1: Successful Commit
```bash
# Pošalji validan batch
curl -X POST /api/quiz/offline-submit \
  -d '{"sessionId": "test", "answers": [...]}'

# Proveri da su svi odgovori upisani
SELECT COUNT(*) FROM UserAnswers WHERE UserId = 1;
# Expected: +10 (svi odgovori)
```

### Test 2: Rollback on Error
```bash
# Simuliraj grešku (invalid questionId)
curl -X POST /api/quiz/offline-submit \
  -d '{"sessionId": "test", "answers": [
    {"questionId": 1, ...},
    {"questionId": 999999, ...} // Invalid!
  ]}'

# Proveri da NIŠTA nije upisano
SELECT COUNT(*) FROM UserAnswers WHERE UserId = 1;
# Expected: +0 (rollback izvršen)
```

### Test 3: Concurrent Requests
```bash
# Terminal 1: Pošalji batch
curl -X POST /api/quiz/offline-submit -d '...'

# Terminal 2: Paralelno pošalji drugi batch
curl -X POST /api/quiz/offline-submit -d '...'

# Oba batch-a treba da uspeju nezavisno
# Proveri da su oba upisana
SELECT COUNT(*) FROM UserAnswers WHERE UserId = 1;
# Expected: +20 (10 + 10)
```

## 📝 Best Practices

### ✅ DO
```csharp
// 1. Uvek koristi transakcije za batch operacije
await using var trx = await db.Database.BeginTransactionAsync();

// 2. Catch exceptions i rollback
try {
    // ...
    await trx.CommitAsync();
}
catch {
    await trx.RollbackAsync();
    throw;
}

// 3. Koristi await using za auto-dispose
await using var trx = ...;
// Transakcija se automatski dispose-uje na kraju
```

### ❌ DON'T
```csharp
// 1. Ne ostavljaj transakciju otvorenu predugo
await using var trx = ...;
await Task.Delay(10000); // ❌ Lock-uje resource-e 10 sekundi!

// 2. Ne zaboravi commit
await db.SaveChangesAsync();
// ❌ Zaboravio si CommitAsync() - podaci se gube!

// 3. Ne koristi nested transakcije bez razloga
await using var trx1 = ...;
await using var trx2 = ...; // ❌ Komplikuje logiku
```

## 🚀 Deployment Notes

### Production Considerations
```csharp
// Dodaj timeout za velike batch-ove
var commandTimeout = db.Database.GetCommandTimeout();
db.Database.SetCommandTimeout(180); // 3 minutes

await using var trx = await db.Database.BeginTransactionAsync();
try {
    // Batch operacije
    await trx.CommitAsync();
}
finally {
    db.Database.SetCommandTimeout(commandTimeout);
}
```

### Monitoring
```csharp
// Log transaction metrics
var sw = Stopwatch.StartNew();
await using var trx = await db.Database.BeginTransactionAsync();
try {
    // ...
    await trx.CommitAsync();
    _logger.LogInformation("Batch commit successful in {ElapsedMs}ms", sw.ElapsedMilliseconds);
}
catch (Exception ex) {
    await trx.RollbackAsync();
    _logger.LogError(ex, "Batch commit failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
}
```

## 📊 Summary

| Feature | Without Transaction | With Transaction |
|---------|---------------------|------------------|
| **Atomicity** | ❌ Partial inserts | ✅ All-or-nothing |
| **Consistency** | ❌ Inconsistent state | ✅ Always consistent |
| **Error Recovery** | ❌ Manual cleanup | ✅ Auto rollback |
| **Performance** | ⚠️ Multiple SaveChanges | ✅ Single commit |
| **Data Integrity** | ❌ Risk of corruption | ✅ Guaranteed |
| **Production Ready** | ❌ Not recommended | ✅ Best practice |

## 🎯 Conclusion

Transakcije su **obavezne** za batch operacije jer:
1. ✅ Garantuju atomičnost - sve ili ništa
2. ✅ Sprečavaju nekonzistentno stanje baze
3. ✅ Omogućavaju lako error recovery
4. ✅ Bolje performanse od multiple commits
5. ✅ Production-ready pattern

**Next Steps**:
1. Zameni stari `QuizEndpoints.cs` sa `QuizEndpoints_Transaction.cs`
2. Testiraj sa error scenarios
3. Monitor transaction timing u production
4. Deploy na Fly.io
