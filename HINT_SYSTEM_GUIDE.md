# Hint System - Complete Guide

## 💡 Overview

Implementiran **completan Hint sistem** koji omogućava korisnicima da dobiju pomoć za pitanja kroz tri tipa hint-ova:
1. **Formula** - Matematička formula potrebna za rešavanje
2. **Clue** - Hint koji vodi ka rešenju
3. **Solution** - Kompletno objašnjenje (najteži hint)

## 📊 System Components

### 1. Question Hints
**Added to Questions table**:
```sql
ALTER TABLE "Questions"
ADD COLUMN "HintFormula" TEXT NULL,
ADD COLUMN "HintClue" TEXT NULL,
ADD COLUMN "HintDifficulty" INT DEFAULT 1;
```

**Properties**:
- `HintFormula` - Matematička formula (npr. "a² + b² = c²")
- `HintClue` - Hint (npr. "Koristi Pitagorinu teoremu")
- `HintDifficulty` - Težina hint-a (1-3)
  - 1 = Easy (formula je dovoljna)
  - 2 = Medium (potreban je clue)
  - 3 = Hard (potrebno kompletno objašnjenje)

### 2. UserHints Table
**Tracks hint usage**:
```sql
CREATE TABLE "UserHints" (
    "Id" BIGSERIAL PRIMARY KEY,
    "UserId" INT NOT NULL,
    "QuestionId" INT NOT NULL,
    "HintType" VARCHAR(50) NOT NULL,
    "UsedAt" TIMESTAMP NOT NULL
);
```

**Indexes**:
- `IX_UserHints_UserId` - Fast user lookups
- `IX_UserHints_User_Question` - Prevent duplicate hints
- `IX_UserHints_UsedAt` - Time-based queries

---

## 🔄 Hint Flow

### Request Hint
```
User → POST /api/hints/get { questionId, hintType }
↓
Server checks:
  1. Does question exist?
  2. Is hint type valid?
  3. Does hint content exist?
↓
Server records usage in UserHints
↓
Server → { hintType, hintContent, hintDifficulty, success }
```

### Hint Types
1. **formula** → `question.HintFormula`
2. **clue** → `question.HintClue`
3. **solution** → `question.Explanation`

---

## 🛠️ API Endpoints

### POST /api/hints/get
**Description**: Get a hint for a question

**Request**:
```json
{
  "questionId": 5,
  "hintType": "formula"
}
```

**Response** (Success):
```json
{
  "hintType": "formula",
  "hintContent": "a² + b² = c²",
  "hintDifficulty": 1,
  "success": true
}
```

**Response** (No hint available):
```json
{
  "hintType": "clue",
  "hintContent": null,
  "hintDifficulty": 2,
  "success": false,
  "message": "No clue hint available for this question"
}
```

**Error** (Invalid hint type):
```json
{
  "error": "Invalid hint type. Use: formula, clue, or solution"
}
```

---

### GET /api/hints/stats
**Description**: Get user's hint usage statistics

**Response**:
```json
{
  "totalHintsUsed": 25,
  "formulaHintsUsed": 10,
  "clueHintsUsed": 8,
  "solutionHintsUsed": 7,
  "averageHintsPerQuestion": 1.5
}
```

---

### GET /api/hints/history?limit=50
**Description**: Get user's hint usage history

**Response**:
```json
[
  {
    "id": 123,
    "questionId": 5,
    "questionText": "Koliko je 5 + 3?",
    "hintType": "formula",
    "usedAt": "2026-01-23T10:30:00Z"
  },
  {
    "id": 122,
    "questionId": 4,
    "questionText": "Rešite jednačinu: 2x + 5 = 15",
    "hintType": "clue",
    "usedAt": "2026-01-23T10:25:00Z"
  }
]
```

---

### GET /api/hints/question/{questionId}
**Description**: Get summary of available hints for a question

**Response**:
```json
{
  "questionId": 5,
  "availableHints": {
    "formula": {
      "available": true,
      "used": false,
      "difficulty": 1
    },
    "clue": {
      "available": true,
      "used": false,
      "difficulty": 1
    },
    "solution": {
      "available": true,
      "used": true,
      "difficulty": 3
    }
  }
}
```

---

## 📝 Example Usage

### Scenario: User Stuck on Question

**Step 1: Check Available Hints**
```bash
curl https://mathlearning-api.fly.dev/api/hints/question/5 \
  -H "Authorization: Bearer TOKEN"
```

**Response**:
```json
{
  "questionId": 5,
  "availableHints": {
    "formula": { "available": true, "used": false },
    "clue": { "available": true, "used": false },
    "solution": { "available": true, "used": false }
  }
}
```

**Step 2: Request Formula Hint**
```bash
curl -X POST https://mathlearning-api.fly.dev/api/hints/get \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"questionId": 5, "hintType": "formula"}'
```

**Response**:
```json
{
  "hintType": "formula",
  "hintContent": "a² + b² = c²",
  "hintDifficulty": 1,
  "success": true
}
```

**Step 3: Still Stuck? Request Clue**
```bash
curl -X POST https://mathlearning-api.fly.dev/api/hints/get \
  -H "Authorization: Bearer TOKEN" \
  -d '{"questionId": 5, "hintType": "clue"}'
```

**Response**:
```json
{
  "hintType": "clue",
  "hintContent": "Primeni Pitagorinu teoremu. Treći ugao je 90°.",
  "hintDifficulty": 2,
  "success": true
}
```

**Step 4: Need Full Solution**
```bash
curl -X POST https://mathlearning-api.fly.dev/api/hints/get \
  -H "Authorization: Bearer TOKEN" \
  -d '{"questionId": 5, "hintType": "solution"}'
```

**Response**:
```json
{
  "hintType": "solution",
  "hintContent": "Koristi Pitagorinu teoremu: a² + b² = c². Za stranice 3 i 4, hipotenuza je √(3² + 4²) = √25 = 5.",
  "hintDifficulty": 3,
  "success": true
}
```

---

## 🎯 Hint Strategy (Progressive Disclosure)

### Level 1: Formula (Easiest)
**When to use**: User needs mathematical formula
**Example**: 
- Question: "Izračunaj hipotenuzu trougla sa stranicama 3 i 4"
- Formula: "a² + b² = c²"

### Level 2: Clue (Medium)
**When to use**: User needs direction/approach
**Example**:
- Question: "Rešite: 2x + 5 = 15"
- Clue: "Oduzmi 5 sa obe strane, zatim podeli sa 2"

### Level 3: Solution (Hardest)
**When to use**: User completely stuck
**Example**:
- Question: "Rešite: 2x + 5 = 15"
- Solution: "2x + 5 = 15 → 2x = 10 → x = 5"

---

## 📊 Analytics & Insights

### Hint Usage Patterns
```sql
-- Users who use most hints
SELECT 
    UserId,
    COUNT(*) as TotalHints,
    COUNT(CASE WHEN HintType = 'solution' THEN 1 END) as SolutionHints
FROM UserHints
GROUP BY UserId
ORDER BY TotalHints DESC
LIMIT 10;
```

### Questions Needing Better Hints
```sql
-- Questions where users frequently need solution hints
SELECT 
    QuestionId,
    COUNT(*) as SolutionRequests,
    ROUND(100.0 * COUNT(*) / (SELECT COUNT(*) FROM UserHints WHERE QuestionId = uh.QuestionId), 2) as SolutionPercentage
FROM UserHints uh
WHERE HintType = 'solution'
GROUP BY QuestionId
HAVING COUNT(*) > 5
ORDER BY SolutionPercentage DESC;
```

---

## 💻 Client Implementation

### React/TypeScript Example
```typescript
interface HintService {
  async getHint(questionId: number, hintType: 'formula' | 'clue' | 'solution'): Promise<HintResponse> {
    const response = await fetch('/api/hints/get', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${this.token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ questionId, hintType })
    });

    return await response.json();
  }

  async getQuestionHints(questionId: number): Promise<QuestionHintsSummary> {
    const response = await fetch(`/api/hints/question/${questionId}`, {
      headers: {
        'Authorization': `Bearer ${this.token}`
      }
    });

    return await response.json();
  }

  async getHintStats(): Promise<HintStats> {
    const response = await fetch('/api/hints/stats', {
      headers: {
        'Authorization': `Bearer ${this.token}`
      }
    });

    return await response.json();
  }
}

// Usage in component
const QuestionHintButton: React.FC<{ questionId: number }> = ({ questionId }) => {
  const [availableHints, setAvailableHints] = useState<any>(null);
  const [currentHint, setCurrentHint] = useState<HintResponse | null>(null);

  useEffect(() => {
    // Load available hints
    hintService.getQuestionHints(questionId).then(setAvailableHints);
  }, [questionId]);

  const requestHint = async (hintType: string) => {
    const hint = await hintService.getHint(questionId, hintType);
    setCurrentHint(hint);
  };

  return (
    <div className="hint-controls">
      <h3>Need Help?</h3>
      
      {availableHints?.availableHints.formula.available && !availableHints.availableHints.formula.used && (
        <button onClick={() => requestHint('formula')}>
          💡 Show Formula
        </button>
      )}
      
      {availableHints?.availableHints.clue.available && !availableHints.availableHints.clue.used && (
        <button onClick={() => requestHint('clue')}>
          🔍 Get Clue
        </button>
      )}
      
      {availableHints?.availableHints.solution.available && !availableHints.availableHints.solution.used && (
        <button onClick={() => requestHint('solution')}>
          📖 Show Solution
        </button>
      )}

      {currentHint && currentHint.success && (
        <div className="hint-display">
          <strong>{currentHint.hintType}:</strong>
          <p>{currentHint.hintContent}</p>
        </div>
      )}
    </div>
  );
};
```

### Blazor Example
```csharp
@inject HintService HintService

<div class="hint-section">
    <h3>Need Help?</h3>
    
    @if (_availableHints != null)
    {
        @if (_availableHints.AvailableHints.Formula.Available && !_availableHints.AvailableHints.Formula.Used)
        {
            <MudButton OnClick="() => RequestHint('formula')" 
                       Variant="Variant.Filled" 
                       Color="Color.Primary">
                💡 Show Formula
            </MudButton>
        }
        
        @if (_availableHints.AvailableHints.Clue.Available && !_availableHints.AvailableHints.Clue.Used)
        {
            <MudButton OnClick="() => RequestHint('clue')" 
                       Variant="Variant.Filled" 
                       Color="Color.Info">
                🔍 Get Clue
            </MudButton>
        }
        
        @if (_availableHints.AvailableHints.Solution.Available && !_availableHints.AvailableHints.Solution.Used)
        {
            <MudButton OnClick="() => RequestHint('solution')" 
                       Variant="Variant.Filled" 
                       Color="Color.Warning">
                📖 Show Solution
            </MudButton>
        }
    }

    @if (_currentHint != null && _currentHint.Success)
    {
        <MudAlert Severity="Severity.Info" Class="mt-4">
            <strong>@_currentHint.HintType:</strong>
            <p>@_currentHint.HintContent</p>
        </MudAlert>
    }
</div>

@code {
    [Parameter] public int QuestionId { get; set; }
    
    private QuestionHintsSummary? _availableHints;
    private HintResponse? _currentHint;

    protected override async Task OnInitializedAsync()
    {
        _availableHints = await HintService.GetQuestionHintsAsync(QuestionId);
    }

    private async Task RequestHint(string hintType)
    {
        _currentHint = await HintService.GetHintAsync(QuestionId, hintType);
        
        // Refresh available hints
        _availableHints = await HintService.GetQuestionHintsAsync(QuestionId);
    }
}
```

---

## 🎓 Gamification Ideas

### Hint Economy
**Reward users for not using hints**:
```
No hints used: +50 XP
Formula only: +30 XP
Clue + Formula: +20 XP
Solution used: +10 XP
```

### Achievements
- **Self-Taught** - Solved 50 questions without hints
- **Formula Master** - Solved 100 questions using only formula hints
- **Persistent Learner** - Used solution hints 50 times (learning is good!)

### Hint Budget
**Daily limit**:
- 10 formula hints/day
- 5 clue hints/day
- 3 solution hints/day

---

## 🧪 Testing

### Test 1: Get Formula Hint
```bash
curl -X POST http://localhost:5000/api/hints/get \
  -H "Authorization: Bearer TOKEN" \
  -d '{"questionId": 1, "hintType": "formula"}'
```

### Test 2: Get Hint Stats
```bash
curl http://localhost:5000/api/hints/stats \
  -H "Authorization: Bearer TOKEN"
```

### Test 3: Get Question Hints Summary
```bash
curl http://localhost:5000/api/hints/question/1 \
  -H "Authorization: Bearer TOKEN"
```

### Test 4: Get Hint History
```bash
curl http://localhost:5000/api/hints/history?limit=10 \
  -H "Authorization: Bearer TOKEN"
```

---

## 🚀 Deployment

```bash
# 1. Apply migration
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api

# 2. Deploy to Fly.io
fly deploy -a mathlearning-api

# 3. Test endpoints
curl -X POST https://mathlearning-api.fly.dev/api/hints/get ...
```

---

## 📊 Database Schema

### Questions Table (Updated)
```sql
CREATE TABLE "Questions" (
    "Id" SERIAL PRIMARY KEY,
    "Text" TEXT NOT NULL,
    "Type" VARCHAR(50) NOT NULL DEFAULT 'multiple_choice',
    "CorrectAnswer" TEXT NULL,
    "Explanation" TEXT NULL,
    "Difficulty" INT NOT NULL DEFAULT 1,
    "CategoryId" INT NOT NULL,
    "SubtopicId" INT NOT NULL,
    -- Hint columns
    "HintFormula" TEXT NULL,
    "HintClue" TEXT NULL,
    "HintDifficulty" INT DEFAULT 1,
    -- Timestamps
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL
);
```

### UserHints Table (New)
```sql
CREATE TABLE "UserHints" (
    "Id" BIGSERIAL PRIMARY KEY,
    "UserId" INT NOT NULL,
    "QuestionId" INT NOT NULL,
    "HintType" VARCHAR(50) NOT NULL,
    "UsedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    FOREIGN KEY ("QuestionId") REFERENCES "Questions"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_UserHints_UserId" ON "UserHints" ("UserId");
CREATE INDEX "IX_UserHints_User_Question" ON "UserHints" ("UserId", "QuestionId");
CREATE INDEX "IX_UserHints_UsedAt" ON "UserHints" ("UsedAt");
```

---

## 🏆 Conclusion

**Hint System** provides:
- ✅ **Better Learning** - Progressive help for students
- ✅ **Reduced Frustration** - Students don't get stuck
- ✅ **Analytics** - Track which questions are hard
- ✅ **Gamification** - Reward hint-free solving
- ✅ **Production-ready** - Scalable, indexed, tracked

Build successful ✅ - Ready for production! 🚀
