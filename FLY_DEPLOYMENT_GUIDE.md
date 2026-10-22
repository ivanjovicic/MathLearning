# Fly.io deployment guide

Verified against the repository configuration on 2026-10-22.

## Current deployment model

- `fly.toml` deploys `mathlearning-api` from `Dockerfile` to port 8080.
- Fly stops the API machine when idle and starts it on demand. Fly checks both `/api/health/` (process liveness) and `/api/health/ready` (traffic readiness).
- The `mathlearning_data` Fly volume is mounted at `/data` for Data Protection keys. The API database is external to Fly; repository configuration identifies Neon PostgreSQL.
- There is no Fly `release_command` and no Postgres service attached to this Fly app.
- Production uses `Database:StartupMode=ValidateExact`. The application fails startup when its schema guard sees pending or unknown migrations; it does not migrate production on startup.

## Health, required configuration, and Redis fallback

- `/api/health/` is deliberately independent of external services. It reports whether the running API process can answer HTTP requests.
- `/api/health/ready` returns `503` unless PostgreSQL is reachable, the startup schema guard reports exact migration alignment, the cosmetic catalog is ready, and Redis is connected when `Redis:Required=true`.
- Both Fly service checks must pass for the proxy to route traffic to the machine. Fly service checks route around an unhealthy machine but do not restart it. `fly.toml` has no explicit restart section; Fly's default is `on-failure`, so startup exceptions must exit non-zero for automatic failure retries.
- Production requires `ConnectionStrings__Default` (Npgsql/PostgreSQL) and `JwtSettings__SecretKey`. Keep their values in Fly secrets; inspect names with `fly secrets list`, never values.
- Data Protection requires `DataProtection__KeysPath` on durable storage plus `DataProtection__CertificatePassword` and exactly one of `DataProtection__CertificatePath` or `DataProtection__CertificateBase64`. `fly.toml` sets the key path to `/data/dataprotection-keys`; Fly currently reports the Base64 certificate and password secret names as configured.
- `Database__StartupMode` must remain `ValidateExact` in production. Do not deploy while the schema guard reports pending or unknown migrations.
- Redis is optional by default. With no Redis connection and `Redis__Required=false`, the API selects `DbFallback` and serves leaderboard requests from PostgreSQL. This is a supported functional mode, with more database work and less cache-backed ranking capacity under load. Set a Redis connection (`ConnectionStrings__Redis` or `Redis__ConnectionString`) and `Redis__Required=true` only when Redis availability is an operational requirement.
- The `mathlearning_data` volume stores ASP.NET Data Protection keys; it is not a database volume or backup. PostgreSQL remains external and must be backed up through its provider.

## Database and deployment order

1. Follow [docs/database-migrations.md](docs/database-migrations.md) for the read-only history and data preflight, backup, disposable rehearsal, reviewed migration script, integrity checks and recovery plan.
2. Obtain the required explicit approval before applying any production SQL. This repository guide does not authorize production migration execution.
3. After the database is verified at the target migration, run the manual GitHub Actions workflow `Deploy to Fly.io` with `confirmDatabaseMigrated=true`. The workflow blocks deployment unless that input is true.
4. Check `fly status -a mathlearning-api`, `fly releases -a mathlearning-api` and `fly checks list -a mathlearning-api`. When the machine is stopped by scale-to-zero, the check can say that the machine has not started; this is not a database schema result.
5. After deployment, request `/api/health/ready` and verify the startup logs show `ValidateExact` with no pending or unknown migrations.

## Deploy and rollback

Run the existing manual workflow only after the database has already been verified against the deployed API migration set:

```powershell
gh workflow run "Deploy to Fly.io" --ref main -f confirmDatabaseMigrated=true
```

Then inspect the workflow result and verify `fly status -a mathlearning-api`, `fly checks list -a mathlearning-api`, and `fly logs -a mathlearning-api`. A request to `/api/health/ready` can wake a stopped machine; use it when an application startup/readiness check is intended.

If the API image fails after deployment, identify the previous known-good image with `fly releases --image -a mathlearning-api`, then redeploy that image:

```powershell
fly deploy --remote-only -a mathlearning-api --image <previous-image-reference>
```

This reuses the current repository Fly configuration and changes the application image only. This application change does not alter the database schema. For releases that include a database migration, do not assume an application image rollback reverses the schema; use the reviewed forward-repair or the provider's verified restore procedure approved for that incident. Verify `/api/health/ready` and the exact schema status after recovery.

Do not run production `dotnet ef database update`, paste hand-written SQL, disable schema validation, or retrieve Fly secret values for use in shell commands. `fly secrets list` reports configured secret names but does not provide the connection string. Keep credentials in the approved secret manager or protected libpq service/password files.

## Safe status commands

```powershell
fly status -a mathlearning-api
fly releases -a mathlearning-api
fly checks list -a mathlearning-api
fly secrets list -a mathlearning-api
```

The health request may wake the stopped machine and connect to the database. Run it only when an application startup check is intended.
