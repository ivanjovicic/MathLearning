# 🚨 Emergency Fix for 500 Login Error

## Issue
The API is returning 500 errors because the database is missing the new leaderboard columns added in the latest update.

## Quick Fix - Apply SQL Migration Directly

### Option 1: Using Fly.io Postgres Console (Recommended)

1. **Connect to your Fly.io Postgres database:**
   ```bash
   fly postgres connect -a <your-postgres-app-name>
   ```

2. **Run the migration script:**
   ```bash
   \i add_leaderboard_columns.sql
   ```

   Or copy-paste the SQL from `add_leaderboard_columns.sql` directly into the console.

### Option 2: Using psql with Connection String

1. **Get your database connection string from Fly.io:**
   ```bash
   fly secrets list -a mathlearning-api
   ```

2. **Connect using psql:**
   ```bash
   psql "your-connection-string-here"
   ```

3. **Run the migration:**
   ```sql
   -- Copy and paste contents of add_leaderboard_columns.sql
   ```

### Option 3: Using pgAdmin or Database GUI

1. Open your database tool
2. Connect to your Fly.io Postgres database
3. Open a new SQL query window
4. Paste the contents of `add_leaderboard_columns.sql`
5. Execute the script

## What the Migration Does

✅ Adds new columns:
- `DailyXp` - XP earned today
- `WeeklyXp` - XP earned this week
- `MonthlyXp` - XP earned this month
- `LastXpResetDate` - Timestamp of last reset
- `LeaderboardOptIn` - Privacy setting (default: true)
- `SchoolId` - Optional school identifier
- `FacultyId` - Optional faculty identifier

✅ Creates performance indexes for fast leaderboard queries

✅ Initializes existing users with default values

## Verification

After applying the migration, verify it worked:

```sql
SELECT 
    "UserId",
    "Username",
    "DailyXp",
    "WeeklyXp",
    "MonthlyXp",
    "LeaderboardOptIn"
FROM "UserProfiles"
LIMIT 5;
```

You should see the new columns populated with default values (0 for XP fields, true for LeaderboardOptIn).

## Test Login Again

After applying the migration:

1. Restart your Fly.io app (or wait for auto-deploy):
   ```bash
   fly apps restart mathlearning-api
   ```

2. Try logging in again from your Flutter app

3. Check if the 500 error is resolved

## Local Development - Disk Space Issue

Your local machine ran out of disk space during migration generation. To fix:

### 1. Clean up temporary files:
```powershell
# Clean dotnet build cache
dotnet nuget locals all --clear

# Clean MSBuild temp files
Remove-Item "$env:TEMP\MSBuildTemp" -Recurse -Force -ErrorAction SilentlyContinue

# Clean dotnet temp files
Remove-Item "$env:TEMP\.dotnet" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:TEMP\nuget" -Recurse -Force -ErrorAction SilentlyContinue
```

### 2. Clean your project:
```powershell
cd c:\Users\Alex\source\repos\Mathlearning\MathLearning
Get-ChildItem -Include bin,obj -Recurse -Force | Remove-Item -Recurse -Force
```

### 3. Check available disk space:
```powershell
Get-PSDrive C | Select-Object Used,Free
```

You should have at least 5-10 GB free for comfortable development.

### 4. Free up more space if needed:
- Empty Recycle Bin
- Run Disk Cleanup (cleanmgr.exe)
- Delete old downloads, temp files
- Uninstall unused applications
- Move large files to external storage

## After Fixing Disk Space

Once you have enough disk space, you can generate the migration properly:

```powershell
cd src/MathLearning.Infrastructure
dotnet ef migrations add AddLeaderboardEnhancements --context ApiDbContext --startup-project ../MathLearning.Api
```

But since you've already applied the manual SQL migration to production, you'll need to create an empty migration locally:

```powershell
# This creates a migration that matches what's already in the database
dotnet ef migrations add AddLeaderboardEnhancements --context ApiDbContext --startup-project ../MathLearning.Api
```

## Summary

1. **Immediate Fix:** Apply `add_leaderboard_columns.sql` to your Fly.io database ✅
2. **Restart API:** `fly apps restart mathlearning-api` ✅
3. **Test Login:** Should now work without 500 error ✅
4. **Fix Local Disk:** Clean up space for future development ✅

The 500 error should be resolved once the database has the new columns!
