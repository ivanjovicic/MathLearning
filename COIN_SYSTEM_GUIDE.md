# 💰 Coin System + Enhanced Hints - Complete Guide

## 🎯 Overview

Implementiran **kompletan coin-based hint sistem** koji dodaje gamification layer kroz:
- 💰 **Coin economy** - Earn & spend coins
- 💡 **4 tipa hint-ova** - Formula, Clue, Eliminate, Solution
- 🏆 **Leaderboards** - Richest users
- 📊 **Coin tracking** - Earned vs Spent

---

## 💰 Coin Economy

### Starting Balance
- New users start with **100 coins**
- Coins can be earned through:
  - ✅ Correct answers (+10 coins)
  - 🔥 Streak bonuses (+5 coins per day)
  - 🎯 Level up rewards (+50 coins)
  - 🎁 Daily login (+20 coins)

### Coin Costs

| Hint Type | Cost | Description |
|-----------|------|-------------|
| **Formula** | 5 coins | Mathematical formula |
| **Clue** | 10 coins | Hint towards solution |
| **Eliminate** | 15 coins | Remove one wrong option |
| **Solution** | 20 coins | Full explanation |

---

## 🛠️ API Endpoints

### Hint Endpoints

#### GET /api/hints/questions/{id}/formula
**Description**: Get formula hint for a question

**Headers**:
```
Authorization: Bearer <token>
```

**Response** (Success - First Time):
```json
{
  "formula": "a² + b² = c²",
  "available": true,
  "cost": 5,
  "remainingCoins": 95
}
```

**Response** (Already Used):
```json
{
  "formula": "a² + b² = c²",
  "available": true,
  "alreadyUsed": true,
  "cost": 0
}
```

**Response** (Insufficient Coins):
```json
{
  "error": "Insufficient coins",
  "required": 5,
  "current": 2
}
```
**Status Code**: 402 Payment Required

---

#### GET /api/hints/questions/{id}/clue
**Description**: Get clue hint

**Response**:
```json
{
  "clue": "Primeni Pitagorinu teoremu za rešavanje",
  "available": true,
  "cost": 10,
  "remainingCoins": 85
}
```

---

#### POST /api/hints/questions/{id}/eliminate
**Description**: Eliminate one wrong option (multiple choice only)

**Response**:
```json
{
  "remainingOptions": ["13", "15", "17"],
  "eliminatedOption": "19",
  "cost": 15,
  "remainingCoins": 70
}
```

**Error** (Already Used):
```json
{
  "error": "Eliminate hint already used for this question"
}
```
**Status Code**: 409 Conflict

---

#### GET /api/hints/questions/{id}/solution
**Description**: Get full solution (most expensive)

**Response**:
```json
{
  "solution": "Koristi Pitagorinu teoremu: a² + b² = c². Za stranice 3 i 4, hipotenuza je √(3² + 4²) = √25 = 5.",
  "available": true,
  "cost": 20,
  "remainingCoins": 50
}
```

---

#### GET /api/hints/question/{questionId}
**Description**: Get summary of available hints for a question

**Response**:
```json
{
  "questionId": 5,
  "currentCoins": 100,
  "availableHints": {
    "formula": {
      "available": true,
      "used": false,
      "cost": 5,
      "affordable": true
    },
    "clue": {
      "available": true,
      "used": false,
      "cost": 10,
      "affordable": true
    },
    "eliminate": {
      "available": true,
      "used": false,
      "cost": 15,
      "affordable": true
    },
    "solution": {
      "available": true,
      "used": false,
      "cost": 20,
      "affordable": true
    }
  }
}
```

---

### Coin Endpoints

#### GET /api/coins/balance
**Description**: Get user's coin balance

**Response**:
```json
{
  "coins": 85,
  "totalEarned": 150,
  "totalSpent": 65,
  "level": 3,
  "xp": 450,
  "streak": 5
}
```

---

#### POST /api/coins/earn?amount=10&reason=correct_answer
**Description**: Earn coins (called after correct answer)

**Response**:
```json
{
  "message": "Earned 10 coins",
  "reason": "correct_answer",
  "newBalance": 95,
  "totalEarned": 160
}
```

---

#### POST /api/coins/spend?amount=20&reason=custom_item
**Description**: Spend coins manually

**Response**:
```json
{
  "message": "Spent 20 coins",
  "reason": "custom_item",
  "newBalance": 75,
  "totalSpent": 85
}
```

---

#### GET /api/coins/history
**Description**: Get coin transaction history

**Response**:
```json
[
  {
    "type": "spent",
    "amount": -10,
    "reason": "Used clue hint",
    "timestamp": "2026-01-23T15:30:00Z"
  },
  {
    "type": "spent",
    "amount": -5,
    "reason": "Used formula hint",
    "timestamp": "2026-01-23T15:25:00Z"
  }
]
```

---

#### GET /api/coins/leaderboard?limit=10
**Description**: Get richest users leaderboard

**Response**:
```json
[
  {
    "rank": 1,
    "username": "mathgenius",
    "coins": 500,
    "level": 10,
    "totalEarned": 2000,
    "totalSpent": 1500
  },
  {
    "rank": 2,
    "username": "brainiac",
    "coins": 450,
    "level": 9,
    "totalEarned": 1800,
    "totalSpent": 1350
  }
]
```

---

## 📊 Database Schema

### UserProfiles Table (New)
```sql
CREATE TABLE "UserProfiles" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL UNIQUE,
    "Username" VARCHAR(256) NOT NULL UNIQUE,
    -- Coins
    "Coins" INT DEFAULT 100,
    "TotalCoinsEarned" INT DEFAULT 0,
    "TotalCoinsSpent" INT DEFAULT 0,
    -- Stats
    "Level" INT DEFAULT 1,
    "Xp" INT DEFAULT 0,
    "Streak" INT DEFAULT 0,
    -- Timestamps
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL
);

CREATE UNIQUE INDEX "UX_UserProfiles_UserId" ON "UserProfiles" ("UserId");
CREATE UNIQUE INDEX "UX_UserProfiles_Username" ON "UserProfiles" ("Username");
```

---

## 🎮 Gamification Flow

### Complete Quiz Flow with Coins

```
1. User starts quiz
   ↓
2. User encounters difficult question
   ↓
3. User checks available hints:
   GET /api/hints/question/5
   → { formula: 5 coins, clue: 10 coins, eliminate: 15 coins }
   ↓
4. User uses formula hint:
   GET /api/hints/questions/5/formula
   → { formula: "a² + b² = c²", cost: 5, remainingCoins: 95 }
   ↓
5. Still stuck? Use clue:
   GET /api/hints/questions/5/clue
   → { clue: "Apply Pythagorean theorem", cost: 10, remainingCoins: 85 }
   ↓
6. Still stuck? Eliminate wrong option:
   POST /api/hints/questions/5/eliminate
   → { remainingOptions: ["13", "15"], cost: 15, remainingCoins: 70 }
   ↓
7. User answers correctly!
   POST /api/coins/earn?amount=10&reason=correct_answer
   → { newBalance: 80, totalEarned: 110 }
```

---

## 💡 Hint Strategy (Progressive Cost)

### Why Progressive Pricing?

**Formula (5 coins)** - Cheapest
- Gives mathematical formula
- User still needs to apply it
- Encourages learning

**Clue (10 coins)** - Medium
- Gives direction/approach
- More help than formula
- Still requires thinking

**Eliminate (15 coins)** - Expensive
- Removes one wrong option
- Increases odds from 25% to 33%
- Big advantage

**Solution (20 coins)** - Most Expensive
- Full explanation
- No thinking required
- Use only when truly stuck

---

## 🎯 Coin Earning Strategies

### 1. Answer Questions Correctly
```
Correct answer: +10 coins
Streak bonus (3 days): +5 coins
Total: +15 coins per question
```

### 2. Daily Login
```
Daily login: +20 coins
Weekly streak (7 days): +50 coins
```

### 3. Level Up
```
Level 2: +50 coins
Level 5: +100 coins
Level 10: +200 coins
```

### 4. Achievements
```
"100 Questions": +100 coins
"No Hints Master": +200 coins
"Streak Champion": +150 coins
```

---

## 📊 Example Scenarios

### Scenario 1: Smart User
```
Starting balance: 100 coins

Day 1:
- Solve 5 questions without hints: +50 coins (10 each)
- Use formula hint on hard question: -5 coins
- Solve remaining questions: +40 coins
- End balance: 185 coins

Day 2:
- Daily login: +20 coins
- Solve 8 questions (2 with clue hints): +80 - 20 = +60 coins
- End balance: 265 coins

Day 3:
- Streak bonus: +5 coins per correct answer
- Solve 10 questions: +150 coins
- End balance: 420 coins
```

### Scenario 2: Hint-Heavy User
```
Starting balance: 100 coins

Day 1:
- Solve 3 questions without hints: +30 coins
- Use solution hints on 4 questions: -80 coins
- End balance: 50 coins

Day 2:
- Daily login: +20 coins
- Can only afford 1 clue hint now
- Must solve questions without hints
- Learns to be more strategic
```

---

## 🧪 Testing

### Test 1: Get Formula Hint
```bash
curl https://mathlearning-api.fly.dev/api/hints/questions/1/formula \
  -H "Authorization: Bearer TOKEN"
```

### Test 2: Eliminate Wrong Option
```bash
curl -X POST https://mathlearning-api.fly.dev/api/hints/questions/1/eliminate \
  -H "Authorization: Bearer TOKEN"
```

### Test 3: Check Coin Balance
```bash
curl https://mathlearning-api.fly.dev/api/coins/balance \
  -H "Authorization: Bearer TOKEN"
```

### Test 4: Earn Coins
```bash
curl -X POST "https://mathlearning-api.fly.dev/api/coins/earn?amount=10&reason=correct_answer" \
  -H "Authorization: Bearer TOKEN"
```

### Test 5: Coin Leaderboard
```bash
curl https://mathlearning-api.fly.dev/api/coins/leaderboard?limit=10
```

---

## 💻 Client Implementation

### React/TypeScript Example
```typescript
class HintService {
  async useFormulaHint(questionId: number): Promise<HintResponse> {
    const response = await fetch(`/api/hints/questions/${questionId}/formula`, {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    if (response.status === 402) {
      const error = await response.json();
      throw new InsufficientCoinsError(error.required, error.current);
    }

    return await response.json();
  }

  async eliminateWrongOption(questionId: number): Promise<EliminateResponse> {
    const response = await fetch(`/api/hints/questions/${questionId}/eliminate`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}` }
    });

    if (response.status === 409) {
      throw new AlreadyUsedError('Eliminate hint already used');
    }

    return await response.json();
  }

  async checkAffordableHints(questionId: number): Promise<HintSummary> {
    const response = await fetch(`/api/hints/question/${questionId}`, {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    return await response.json();
  }

  async getCoinBalance(): Promise<CoinBalance> {
    const response = await fetch('/api/coins/balance', {
      headers: { 'Authorization': `Bearer ${token}` }
    });

    return await response.json();
  }
}

// Usage in component
const QuizQuestion: React.FC<{ question: Question }> = ({ question }) => {
  const [hints, setHints] = useState<HintSummary | null>(null);
  const [coins, setCoins] = useState<number>(0);

  useEffect(() => {
    // Load hints and coins
    hintService.checkAffordableHints(question.id).then(setHints);
    hintService.getCoinBalance().then(data => setCoins(data.coins));
  }, [question.id]);

  const useHint = async (type: HintType) => {
    try {
      const result = await hintService.useHint(question.id, type);
      setCoins(result.remainingCoins);
      showHintModal(result.content);
    } catch (error) {
      if (error instanceof InsufficientCoinsError) {
        showInsufficientCoinsModal(error.required, error.current);
      }
    }
  };

  return (
    <div>
      <h2>{question.text}</h2>
      
      <div className="coin-display">
        💰 {coins} coins
      </div>

      <div className="hint-buttons">
        {hints?.availableHints.formula.available && (
          <button 
            onClick={() => useHint('formula')}
            disabled={!hints.availableHints.formula.affordable}
          >
            💡 Formula (5 coins)
          </button>
        )}
        
        {hints?.availableHints.eliminate.available && (
          <button 
            onClick={() => useHint('eliminate')}
            disabled={!hints.availableHints.eliminate.affordable}
          >
            ❌ Eliminate (15 coins)
          </button>
        )}
      </div>
    </div>
  );
};
```

---

## 🏆 Achievements & Rewards

### Coin-Based Achievements
- **Thrifty**: Solve 50 questions without using hints (+100 coins)
- **Big Spender**: Spend 500 coins on hints (+50 coins reward)
- **Coin Collector**: Earn 1000 total coins (+200 coins bonus)
- **Balanced**: Maintain 100+ coins for 7 days (+150 coins)

---

## 🚀 Deployment

```bash
# 1. Apply migration
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api

# 2. Deploy
fly deploy -a mathlearning-api

# 3. Create user profiles for existing users (manual migration)
# Run SQL script to create UserProfile for each IdentityUser
INSERT INTO "UserProfiles" ("UserId", "Username", "Coins", "CreatedAt", "UpdatedAt")
SELECT 
    ABS(HASHTEXT("Id")) as "UserId",
    "UserName" as "Username",
    100 as "Coins",
    NOW() as "CreatedAt",
    NOW() as "UpdatedAt"
FROM "AspNetUsers"
WHERE NOT EXISTS (
    SELECT 1 FROM "UserProfiles" WHERE "UserId" = ABS(HASHTEXT("AspNetUsers"."Id"))
);
```

---

## 🎯 Best Practices

### ✅ DO
1. **Show coin cost** before using hint
2. **Confirm expensive hints** (solution = 20 coins)
3. **Display remaining coins** prominently
4. **Encourage earning** - Show ways to earn coins
5. **Track spending** - Show coin history

### ❌ DON'T
1. **Don't let coins go negative** - Check before deducting
2. **Don't charge for already-used hints** - Free on repeat use
3. **Don't make coins too easy** - Balance is key
4. **Don't hide costs** - Transparency is important

---

## 🏆 Conclusion

**Coin System** provides:
- ✅ **Gamification** - Engaging economy
- ✅ **Strategic Thinking** - When to use hints?
- ✅ **Learning Incentive** - Rewards for solving without hints
- ✅ **Fairness** - Everyone starts with 100 coins
- ✅ **Progression** - Earn more as you level up
- ✅ **Production-ready** - Scalable, indexed, tracked

Build successful ✅ - Ready for production! 💰🚀
