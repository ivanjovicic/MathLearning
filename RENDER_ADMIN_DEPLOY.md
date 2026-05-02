# Render Admin Deploy

`MathLearning.Admin` should be deployed as a separate Render Web Service.

That is the safest setup for this repository because:

- the existing root `Dockerfile` publishes only `MathLearning.Api`
- the admin app is a server-side Blazor app with its own auth cookies and admin identity database
- keeping API and Admin as separate services avoids risky reverse-proxy or app-merging work

## What to create on Render

Create a new **Web Service** from the same Git repository with these settings:

- **Runtime:** Docker
- **Dockerfile Path:** `./Dockerfile.admin`
- **Branch:** your deployment branch
- **Health Check Path:** `/health`

After deploy, the admin UI will be available at:

- `/` for the dashboard
- `/login-page` for the standard login page
- `/login` for the Blazor login route

## Required environment variables

Set these environment variables on the Render admin service:

- `ConnectionStrings__AdminIdentity`
- `SeedAdmin__Enabled=true`
- `SeedAdmin__Username=admin`
- `SeedAdmin__Password=<strong-password>`
- `SeedAdmin__Email=admin@mathlearning.com`
- `DataProtection__KeysPath=/var/data/keys`

Optional:

- `SeedAdmin__ResetPasswordOnStart=true`

Use `SeedAdmin__ResetPasswordOnStart=true` only for the first deploy or when you intentionally want to rotate the admin password. After a successful deploy, set it back to `false`.

## Persistent disk

Attach a persistent disk to the admin service and mount it at:

- `/var/data`

This keeps ASP.NET Data Protection keys stable across deploys and restarts, so existing auth cookies do not break every time the service restarts.

`MathLearning.Admin` uses `DataProtection__KeysPath` when it is configured. If you omit it, the app falls back to the admin database key table (`DataProtectionKeys`) and migrations must be applied before the first request.

If Render logs show `relation "DataProtectionKeys" does not exist`, check that:

- `DataProtection__KeysPath=/var/data/keys` is set on the admin service
- the persistent disk is mounted at `/var/data`
- the service was redeployed after changing environment variables

## Database recommendation

Use a dedicated Postgres database for admin identity, for example `mathlearning_admin`.

Recommended shape:

- API service uses its existing application database
- Admin service uses a separate admin identity database

If you use the same Postgres instance for both, still keep the admin data in a separate database instead of mixing it into the API database.

## First login

If `SeedAdmin__Enabled=true` and `SeedAdmin__Password` is configured, the app will create the admin user on startup if it does not already exist.

Default username if not overridden:

- `admin`

Open the deployed admin service root URL or `/login-page` and sign in with the configured credentials.
