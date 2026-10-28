# Fly.io database migration instructions

This file replaces the old copy-and-paste SQL and automatic startup migration instructions. Do not use the SQL formerly stored here or run `dotnet ef database update` against production.

The API uses PostgreSQL through Npgsql. Production uses `Database:StartupMode=ValidateExact`; startup migrations are disabled. Use the reviewed procedure in [docs/database-migrations.md](docs/database-migrations.md), including its read-only preflight, verified backup, rehearsal, migration script and post-migration checks.

The production database is documented as Neon PostgreSQL. Fly secrets contain the API connection string; `fly secrets list` shows secret names only. Never print or copy secret values into a repository, shell history or command-line arguments.

Production migration execution requires separate explicit authorization. Deploy the API only after the migration is verified, using the manual `Deploy to Fly.io` workflow and its `confirmDatabaseMigrated=true` guard.
