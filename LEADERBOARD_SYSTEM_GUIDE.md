# 🏆 Enhanced Leaderboard System - Implementation Guide

## 📋 Overview

This document describes the enhanced leaderboard system implementation for MathLearning, featuring multi-scope and multi-period leaderboards with automatic XP tracking.

## ✨ Features

### 1. **Multi-Scope Leaderboards**
- **Global**: All users who opted in
- **School**: Users from the same school
- **Faculty**: Users from the same faculty
- **Friends**: User's friends only

### 2. **Multi-Period Tracking**
- **All-time**: Total XP accumulated
- **Weekly**: XP earned this week (Monday-Sunday)
- **Monthly**: XP earned this month
- **Daily**: XP earned today

### 3. **Privacy Controls**
- Users can opt-in/opt-out of leaderboards via `LeaderboardOptIn` property
- Default: opted-in

### 4. **Automatic XP Reset**
- Background service automatically resets time-based XP counters
- Runs every hour
- Daily XP resets at midnight
- Weekly XP resets on Monday
- Monthly XP resets on the 1st of each month

## 🏗️ Architecture

### New Components

1. **UserProfile Entity Extensions** ([UserProfile.cs](src/MathLearning.Domain/Entities/UserProfile.cs))
   - `DailyXp`, `WeeklyXp`, `MonthlyXp` - Time-based XP counters
   - `LastXpResetDate` - Tracks last reset time
   - `LeaderboardOptIn` - Privacy setting
   - `SchoolId`, `FacultyId` - Optional education organization IDs

2. **DTOs** ([LeaderboardDtos.cs](src/MathLearning.Application/DTOs/Leaderboard/LeaderboardDtos.cs))
   - `LeaderboardItemDto` - Single leaderboard entry
   - `LeaderboardResponseDto` - Complete leaderboard with user's position

3. **Services**
   - `LeaderboardService` ([LeaderboardService.cs](src/MathLearning.Infrastructure/Services/LeaderboardService.cs))
     - Retrieves leaderboard data with filtering
     - Calculates user rankings
   
   - `XpTrackingService` ([XpTrackingService.cs](src/MathLearning.Infrastructure/Services/XpTrackingService.cs))
     - Manages XP addition across all time periods
     - Auto-resets expired counters on XP addition
   
   - `XpResetBackgroundService` ([XpResetBackgroundService.cs](src/MathLearning.Api/Services/XpResetBackgroundService.cs))
     - Background worker that periodically resets XP counters
     - Runs every hour

4. **Endpoints** ([LeaderboardEndpoints.cs](src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs))
   - `GET /api/leaderboard` - New enhanced endpoint
   - `GET /api/leaderboard/global` - Legacy endpoint (backward compatible)
   - `GET /api/leaderboard/friends` - Legacy endpoint (backward compatible)

## 🔧 Database Changes

### New Columns in `UserProfiles` Table

```sql
-- Time-based XP tracking
ALTER TABLE "UserProfiles" ADD COLUMN "DailyXp" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserProfiles" ADD COLUMN "WeeklyXp" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserProfiles" ADD COLUMN "MonthlyXp" integer NOT NULL DEFAULT 0;
ALTER TABLE "UserProfiles" ADD COLUMN "LastXpResetDate" timestamp with time zone;

-- Leaderboard settings
ALTER TABLE "UserProfiles" ADD COLUMN "LeaderboardOptIn" boolean NOT NULL DEFAULT true;
ALTER TABLE "UserProfiles" ADD COLUMN "SchoolId" integer;
ALTER TABLE "UserProfiles" ADD COLUMN "FacultyId" integer;

-- Performance indexes
CREATE INDEX "IX_UserProfiles_Leaderboard_TotalXp" ON "UserProfiles" ("LeaderboardOptIn", "Xp");
CREATE INDEX "IX_UserProfiles_Leaderboard_WeeklyXp" ON "UserProfiles" ("LeaderboardOptIn", "WeeklyXp");
CREATE INDEX "IX_UserProfiles_Leaderboard_MonthlyXp" ON "UserProfiles" ("LeaderboardOptIn", "MonthlyXp");
CREATE INDEX "IX_UserProfiles_Leaderboard_DailyXp" ON "UserProfiles" ("LeaderboardOptIn", "DailyXp");
CREATE INDEX "IX_UserProfiles_School_Leaderboard" ON "UserProfiles" ("SchoolId", "LeaderboardOptIn");
CREATE INDEX "IX_UserProfiles_Faculty_Leaderboard" ON "UserProfiles" ("FacultyId", "LeaderboardOptIn");
```

## 📡 API Usage

### Enhanced Leaderboard Endpoint

```http
GET /api/leaderboard?scope=global&period=week&limit=50
Authorization: Bearer {jwt_token}
```

**Query Parameters:**
- `scope` (optional, default: "global")
  - `global` - All users
  - `school` - Same school
  - `faculty` - Same faculty
  - `friends` - Friends only
  
- `period` (optional, default: "all_time")
  - `all_time` - Total XP
  - `week` - Weekly XP
  - `month` - Monthly XP
  - `day` - Daily XP
  
- `limit` (optional, default: 50)
  - Maximum number of top users to return

**Response:**
```json
{
  "scope": "global",
  "period": "week",
  "items": [
    {
      "rank": 1,
      "userId": "123",
      "displayName": "JohnDoe",
      "avatarUrl": "https://...",
      "score": 1250,
      "streakDays": 7,
      "level": 15
    }
  ],
  "me": {
    "rank": 42,
    "userId": "current-user-id",
    "displayName": "CurrentUser",
    "avatarUrl": "https://...",
    "score": 850,
    "streakDays": 3,
    "level": 10
  }
}
```

## 🎯 Usage in Application Code

### Adding XP to User

When a user earns XP (e.g., completing a quiz), use `XpTrackingService`:

```csharp
// In your quiz completion logic
public class QuizEndpoint
{
    private readonly XpTrackingService _xpService;

    public async Task CompleteQuiz(string userId, int earnedXp)
    {
        // This automatically updates all time periods
        var profile = await _xpService.AddXpAsync(userId, earnedXp);
        
        // Profile now has updated:
        // - Xp (total)
        // - DailyXp
        // - WeeklyXp
        // - MonthlyXp
        // - Level (auto-calculated)
    }
}
```

### Retrieving Leaderboard

```csharp
public class LeaderboardController
{
    private readonly LeaderboardService _leaderboardService;

    public async Task<LeaderboardResponseDto> GetLeaderboard(
        string userId,
        string scope = "global",
        string period = "week")
    {
        return await _leaderboardService.GetLeaderboardAsync(
            userId, 
            scope, 
            period, 
            limit: 50
        );
    }
}
```

## 🔄 Migration Steps

1. **Generate Migration**
   ```powershell
   cd src/MathLearning.Infrastructure
   dotnet ef migrations add AddLeaderboardEnhancements --context ApiDbContext --startup-project ../MathLearning.Api
   ```

2. **Review Migration**
   - Check generated migration file
   - Verify all columns and indexes are included

3. **Apply Migration**
   ```powershell
   dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api
   ```

   Or use the automatic migration on app startup (already configured in Program.cs)

4. **Seed Default Values** (optional)
   ```sql
   -- If needed, backfill existing users with default values
   UPDATE "UserProfiles" 
   SET "LeaderboardOptIn" = true,
       "DailyXp" = 0,
       "WeeklyXp" = 0,
       "MonthlyXp" = 0
   WHERE "LeaderboardOptIn" IS NULL;
   ```

## 🎨 Improvements Over Original Design

1. **Performance Optimizations**
   - Added compound indexes for common query patterns
   - Indexes on (LeaderboardOptIn, XP_field) for fast filtering
   - School/Faculty indexes for scoped queries

2. **Privacy First**
   - Users can opt-out of leaderboards
   - Default opt-in for better engagement

3. **Automatic Maintenance**
   - Background service handles XP resets
   - No manual intervention needed
   - Resilient to restarts (checks reset dates)

4. **Backward Compatibility**
   - Legacy endpoints still work
   - Gradual migration path for clients

5. **Flexible Architecture**
   - Separate service layer for business logic
   - Easy to extend with new scopes/periods
   - Testable components

6. **Smart XP Tracking**
   - Auto-detects expired periods when adding XP
   - Prevents stale data
   - Accurate time-based rankings

## 🧪 Testing Recommendations

1. **Unit Tests**
   - Test `LeaderboardService.GetLeaderboardAsync()` with different scopes/periods
   - Test `XpTrackingService.AddXpAsync()` across day/week/month boundaries
   - Test reset logic in `XpResetBackgroundService`

2. **Integration Tests**
   - Test complete leaderboard retrieval flow
   - Test XP addition and leaderboard updates
   - Test opt-in/opt-out scenarios

3. **Load Tests**
   - Test leaderboard queries with large user bases
   - Verify index effectiveness
   - Monitor query performance

## 📊 Performance Considerations

### Query Optimization
- All leaderboard queries use indexes
- Filters applied before ordering
- Limit applied to reduce result set size

### Caching Opportunities (Future Enhancement)
```csharp
// Add caching layer for frequently accessed leaderboards
public class CachedLeaderboardService
{
    private readonly IMemoryCache _cache;
    private readonly LeaderboardService _leaderboardService;

    public async Task<LeaderboardResponseDto> GetLeaderboardAsync(...)
    {
        var cacheKey = $"leaderboard:{scope}:{period}";
        
        if (!_cache.TryGetValue(cacheKey, out LeaderboardResponseDto result))
        {
            result = await _leaderboardService.GetLeaderboardAsync(...);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        }
        
        return result;
    }
}
```

## 🔒 Security Considerations

1. **Authorization**
   - All endpoints require authentication
   - Users can only see leaderboards for their authorized scopes

2. **Privacy**
   - Respect `LeaderboardOptIn` setting
   - Don't expose user IDs externally (use opaque identifiers if needed)

3. **Rate Limiting**
   - Consider rate limiting leaderboard queries
   - Prevent abuse of ranking calculations

## 📝 Future Enhancements

1. **Social Features**
   - Add achievements/badges
   - Add leaderboard challenges
   - Team leaderboards

2. **Advanced Analytics**
   - Historical leaderboard positions
   - XP gain trends
   - Comparative analytics

3. **Notifications**
   - Notify users when they reach new ranks
   - Weekly leaderboard summaries
   - Friend activity updates

4. **Customization**
   - Per-topic leaderboards
   - Custom time ranges
   - Adjustable XP formulas

## 🎉 Summary

The enhanced leaderboard system provides:
- ✅ Flexible scope and period filtering
- ✅ Automatic XP tracking and resets
- ✅ Privacy controls
- ✅ High performance with proper indexing
- ✅ Backward compatibility
- ✅ Easy integration into existing code
- ✅ Maintainable and testable architecture

All components are production-ready and follow best practices for ASP.NET Core applications.
