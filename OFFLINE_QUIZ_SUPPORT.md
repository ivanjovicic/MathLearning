# Offline Quiz Support - API Documentation

## 📱 Offline Functionality

API sada podržava offline rad kroz batch sinhronizaciju odgovora. Kada korisnik radi offline, može da čuva odgovore lokalno i da ih sinhronizuje kasnije kada se vrati online.

## 📤 Offline Batch Submit Endpoint

### Endpoint
```
POST /api/quiz/offline-submit
```

### Authorization
Bearer Token (JWT) required

### Request Body
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "answers": [
    {
      "questionId": 1,
      "answer": "42",
      "isCorrectOffline": true,
      "timeSpent": 15,
      "answeredAt": "2026-01-22T17:30:00Z"
    },
    {
      "questionId": 2,
      "answer": "Option A",
      "isCorrectOffline": false,
      "timeSpent": 25,
      "answeredAt": "2026-01-22T17:31:00Z"
    }
  ]
}
```

### Response Body
```json
{
  "importedCount": 2,
  "newXp": 150,
  "newLevel": 2,
  "streak": 3
}
```

## 🔧 Request DTO Definition

### OfflineAnswerDto
```csharp
public record OfflineAnswerDto(
    int QuestionId,           // ID pitanja
    string Answer,            // Odgovor korisnika
    bool IsCorrectOffline,    // Da li je offline validacija pokazala tačan odgovor
    int TimeSpent,            // Vreme provedeno u sekundama
    DateTime AnsweredAt       // Kada je odgovor dat (UTC)
);
```

### OfflineBatchSubmitRequest
```csharp
public record OfflineBatchSubmitRequest(
    string SessionId,                    // Quiz session ID (GUID)
    List<OfflineAnswerDto> Answers       // Lista odgovora za sinhronizaciju
);
```

### OfflineBatchSubmitResponse
```csharp
public record OfflineBatchSubmitResponse(
    int ImportedCount,    // Broj uspešno importovanih odgovora
    int NewXp,            // Ukupan XP nakon sinhronizacije
    int NewLevel,         // Novi level korisnika
    int Streak           // Trenutni streak (broj uzastopnih dana)
);
```

## 🎯 Kako Radi

### 1. Client Side (Offline)
```typescript
// 1. Korisnik radi kviz offline
const offlineAnswers: OfflineAnswerDto[] = [];

// 2. Čuvaj svaki odgovor lokalno
offlineAnswers.push({
  questionId: 1,
  answer: "42",
  isCorrectOffline: checkAnswerOffline(question, "42"),
  timeSpent: 15,
  answeredAt: new Date().toISOString()
});

// 3. Kad se vrati online, sinhronizuj
await fetch('/api/quiz/offline-submit', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    sessionId: currentSessionId,
    answers: offlineAnswers
  })
});
```

### 2. Server Side
1. **Validacija Session**: Proveri da li session postoji, ako ne - kreiraj novi
2. **Server-side Validacija**: Ne veruj `isCorrectOffline` od klijenta - validuj ponovo na serveru
3. **Batch Insert**: Dodaj sve odgovore odjednom u `UserAnswers` tabelu
4. **Update Stats**: Ažuriraj `UserQuestionStats` za sva pitanja
5. **Calculate Metrics**: Izračunaj XP, Level i Streak
6. **Return Summary**: Vrati statistiku korisniku

## 🔐 Sigurnosne Mere

1. **Server-side Validacija**: 
   - `isCorrectOffline` se ignoriše
   - Server ponovo validira svaki odgovor sa pravim pitanjem iz baze

2. **User Authorization**: 
   - Samo autorizovani korisnici mogu da šalju odgovore
   - Session mora pripadati trenutnom korisniku

3. **Data Integrity**:
   - Provera da li pitanje postoji u bazi
   - Validacija tipa odgovora (multiple choice vs. free text)

## 📊 Optimizacije

### Batch Operations
- **Bulk Insert**: Svi odgovori se dodaju odjednom pre `SaveChangesAsync()`
- **Dictionary Lookup**: Pitanja se učitavaju jednom i čuvaju u dictionary-ju
- **Single Stats Query**: Statistike se učitavaju za sva pitanja odjednom

### Performance
```csharp
// ✅ Dobro - Batch operacija
var questions = await db.Questions
    .Include(q => q.Options)
    .Where(q => questionIds.Contains(q.Id))
    .ToDictionaryAsync(q => q.Id);

// ❌ Loše - N+1 problem
foreach (var answer in answers) {
    var question = await db.Questions.FindAsync(answer.QuestionId);
}
```

## 🧪 Testing

### Test Case 1: Basic Offline Submit
```bash
curl -X POST https://mathlearning-api.fly.dev/api/quiz/offline-submit \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "answers": [
      {
        "questionId": 1,
        "answer": "42",
        "isCorrectOffline": true,
        "timeSpent": 15,
        "answeredAt": "2026-01-22T17:30:00Z"
      }
    ]
  }'
```

### Test Case 2: Multiple Answers
```bash
# Multiple odgovora sa različitim vremenima
curl -X POST https://mathlearning-api.fly.dev/api/quiz/offline-submit \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "new-session-id",
    "answers": [
      {"questionId": 1, "answer": "A", "isCorrectOffline": true, "timeSpent": 10, "answeredAt": "2026-01-22T10:00:00Z"},
      {"questionId": 2, "answer": "B", "isCorrectOffline": false, "timeSpent": 20, "answeredAt": "2026-01-22T10:01:00Z"},
      {"questionId": 3, "answer": "C", "isCorrectOffline": true, "timeSpent": 15, "answeredAt": "2026-01-22T10:02:00Z"}
    ]
  }'
```

### Test Case 3: Empty Answers
```bash
# Trebalo bi da vrati 400 Bad Request
curl -X POST https://mathlearning-api.fly.dev/api/quiz/offline-submit \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "answers": []
  }'
```

## 🔄 Client Implementation Example

### React/TypeScript
```typescript
interface OfflineAnswer {
  questionId: number;
  answer: string;
  isCorrectOffline: boolean;
  timeSpent: number;
  answeredAt: string;
}

class OfflineQuizManager {
  private answers: OfflineAnswer[] = [];
  private sessionId: string;

  constructor(sessionId: string) {
    this.sessionId = sessionId;
    this.loadFromStorage();
  }

  addAnswer(answer: OfflineAnswer) {
    this.answers.push(answer);
    this.saveToStorage();
  }

  async sync(token: string): Promise<void> {
    if (this.answers.length === 0) return;

    const response = await fetch('/api/quiz/offline-submit', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        sessionId: this.sessionId,
        answers: this.answers
      })
    });

    if (response.ok) {
      const result = await response.json();
      console.log('Synced:', result);
      this.clearAnswers();
    }
  }

  private saveToStorage() {
    localStorage.setItem('offline-answers', JSON.stringify(this.answers));
  }

  private loadFromStorage() {
    const stored = localStorage.getItem('offline-answers');
    if (stored) {
      this.answers = JSON.parse(stored);
    }
  }

  private clearAnswers() {
    this.answers = [];
    localStorage.removeItem('offline-answers');
  }
}
```

## 📝 Best Practices

1. **Periodic Sync**: Sinhronizuj periodično (npr. svaki sat) ako je online
2. **Retry Logic**: Implementiraj retry sa exponential backoff
3. **Local Storage**: Čuvaj odgovore u localStorage/IndexedDB
4. **Validation**: Validuj odgovore i offline (za UX) i online (za sigurnost)
5. **Timestamp Precision**: Koristi UTC timestamp za cross-timezone compatibility

## 🚀 Future Improvements

- [ ] Conflict resolution za duplirane odgovore
- [ ] Partial sync - sinhronizuj samo nesinhronizovane odgovore
- [ ] Compression za velike batch-ove
- [ ] Webhook za real-time sync notifikacije
- [ ] Analytics za offline usage patterns
