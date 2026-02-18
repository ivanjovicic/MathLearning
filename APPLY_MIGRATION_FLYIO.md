# Quick Fix: Apply SQL Migration to Fly.io

## Method 1: Fly.io Dashboard (Recommended if no CLI)

1. Go to https://fly.io/dashboard
2. Select your `mathlearning-api` app
3. Go to the Postgres database attached to it
4. Click on "Monitoring" or look for database connection details
5. Use the connection string with any PostgreSQL client

## Method 2: Install Fly CLI and Apply

```powershell
# Install Fly CLI
powershell -Command "iwr https://fly.io/install.ps1 -useb | iex"

# Login to Fly
fly auth login

# List your apps
fly apps list

# Connect to Postgres (replace with your postgres app name)
fly postgres connect -a <postgres-app-name>

# Then paste the SQL from add_leaderboard_columns.sql
```

## Method 3: Use Connection String Directly

If you have your Postgres connection string from Fly secrets:

```powershell
# Get connection string
fly secrets list -a mathlearning-api

# Use pgAdmin, DBeaver, or any PostgreSQL client to connect
# Then run the SQL from add_leaderboard_columns.sql
```

## Method 4: Automatic Migration on Next Deploy

Since your code has auto-migration enabled in Program.cs, you can also:

1. Commit and push your changes to Git
2. Let Fly.io auto-deploy
3. The app will run migrations on startup

But this means your API will be down until deployment completes.

## 🚨 THE SQL TO RUN:

Copy this entire block into your PostgreSQL console:

```sql
BEGIN;

ALTER TABLE "UserProfiles" 
    ADD COLUMN IF NOT EXISTS "DailyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "WeeklyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "MonthlyXp" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LastXpResetDate" timestamp with time zone,
    ADD COLUMN IF NOT EXISTS "LeaderboardOptIn" boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS "SchoolId" integer,
    ADD COLUMN IF NOT EXISTS "FacultyId" integer;

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

After running, restart your API:
```powershell
fly apps restart mathlearning-api
```

Then test login again - it should work! ✅
