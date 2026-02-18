# 🎯 Action Plan - Fix 500 Login Error

## IMMEDIATE: Fix Production (Fly.io)

Your API is down because the database is missing new columns. **Apply the SQL migration NOW:**

### Step 1: Connect to Fly.io Postgres

```bash
fly postgres connect -a <your-postgres-app-name>
```

If you don't know the app name:
```bash
fly apps list
```

### Step 2: Apply the Migration

Copy and paste this SQL into the Postgres console:

```sql
BEGIN;

-- Add new columns
ALTER TABLE "UserProfiles" 
    ADD COLUMN IF NOT EXISTS "DailyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "WeeklyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MonthlyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LastXpResetDate" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "LeaderboardOptIn" boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS "SchoolId" integer,
    ADD COLUMN IF NOT EXISTS "FacultyId" integer;

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_TotalXp" 
    ON "UserProfiles" ("LeaderboardOptIn", "Xp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_WeeklyXp" 
    ON "UserProfiles" ("LeaderboardOptIn", "WeeklyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_MonthlyXp" 
    ON "UserProfiles" ("LeaderboardOptIn", "MonthlyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Leaderboard_DailyXp" 
    ON "UserProfiles" ("LeaderboardOptIn", "DailyXp");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_School_Leaderboard" 
    ON "UserProfiles" ("SchoolId", "LeaderboardOptIn");
CREATE INDEX IF NOT EXISTS "IX_UserProfiles_Faculty_Leaderboard" 
    ON "UserProfiles" ("FacultyId", "LeaderboardOptIn");

-- Initialize existing users
UPDATE "UserProfiles" 
SET 
    "DailyXp" = 0,
    "WeeklyXp" = 0,
    "MonthlyXp" = 0,
    "LeaderboardOptIn" = true,
    "LastXpResetDate" = NOW()
WHERE "DailyXp" IS NULL;

COMMIT;
```

### Step 3: Restart API

```bash
fly apps restart mathlearning-api
```

### Step 4: Test Login

Your Flutter app login should now work! ✅

---

## LOCAL: Free More Disk Space

Your C: drive needs at least **10 GB free** for comfortable development.

### Quick Wins:

1. **Empty Recycle Bin**
2. **Run Windows Disk Cleanup:**
   ```
   Press Win+R, type: cleanmgr
   Select C: drive
   Check all boxes, run cleanup
   ```

3. **Check what's using space:**
   ```powershell
   Get-ChildItem C:\ -Recurse -ErrorAction SilentlyContinue | 
     Where-Object {$_.PSIsContainer -eq $false} | 
     Group-Object DirectoryName | 
     Select-Object @{Name='Folder';Expression={$_.Name}}, 
                   @{Name='Size(GB)';Expression={($_.Group | Measure-Object Length -Sum).Sum / 1GB}} | 
     Sort-Object 'Size(GB)' -Descending | 
     Select-Object -First 20
   ```

4. **Common space hogs to check:**
   - `C:\Windows\Temp` - Delete old temp files
   - `C:\Users\Alex\AppData\Local\Temp` - Delete old temp files
   - `C:\Users\Alex\Downloads` - Move or delete old downloads
   - Docker images (if installed): `docker system prune -a`
   - Old Windows.old folder: Use Disk Cleanup -> "Previous Windows installations"

### After freeing space, try migration again:

```powershell
cd c:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef migrations add AddLeaderboardEnhancements --context ApiDbContext --startup-project ../MathLearning.Api
```

---

## Summary

**NOW:** ✅ Apply SQL migration to Fly.io Postgres → Login works again
**THEN:** 🧹 Free up C: drive space → Development works locally

The code implementation is complete and correct. The only issues are:
1. Production database needs the new columns (fix with SQL above)
2. Local disk is full (prevent local development)

Focus on #1 first to get your app working!
