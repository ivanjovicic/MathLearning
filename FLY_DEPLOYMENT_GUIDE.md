# 🚀 Fly.io Deployment Guide

## ❌ Problem

```bash
PS C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure> fly deploy
Error: the config for your app is missing an app name, add an app field to the fly.toml file or specify with the -a flag
```

**Uzrok**: Pogrešan direktorijum! Moraš biti u **root** direktorijumu gde se nalazi `fly.toml`.

---

## ✅ Rešenje

### Step 1: Navigiraj do Root Direktorijuma
```bash
# Trenutno si ovde (POGREŠNO):
PS C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure>

# Idi ovde (TAČNO):
cd ..\..
# Ili
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning
```

### Step 2: Verifikuj fly.toml
```bash
# Proveri da li postoji fly.toml u trenutnom direktorijumu
ls fly.toml
```

**Expected output**:
```
-a----        1/23/2026   3:00 PM            500 fly.toml
```

### Step 3: Deploy
```bash
fly deploy
```

---

## 📋 Pre-Deployment Checklist

### 1. ✅ Verifikuj Connection String
**Lokacija**: Fly.io Secrets (ne u kodu!)

```bash
# Set connection string secret
fly secrets set ConnectionStrings__Default="Host=ep-wispy-smoke-ag4qtxhe-pooler.c-2.eu-central-1.aws.neon.tech;Port=5432;Username=neondb_owner;Password=<NEON_PASSWORD>;Database=neondb;SSL Mode=Require;Trust Server Certificate=true;"
```

### 2. ✅ Set JWT Settings
```bash
fly secrets set JwtSettings__SecretKey="YourSuperSecretKeyThatIsAtLeast32CharactersLong!"
fly secrets set JwtSettings__Issuer="MathLearningAPI"
fly secrets set JwtSettings__Audience="MathLearningApp"
```

### 3. ✅ Set ASPNETCORE_ENVIRONMENT
```bash
fly secrets set ASPNETCORE_ENVIRONMENT="Production"
```

### 4. ✅ Verifikuj Migrations
```bash
# Apply all pending migrations
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api
```

**Expected migrations**:
- ✅ `InitialCreate`
- ✅ `AddRefreshTokens`
- ✅ `AddHintSystem`
- ✅ `AddUserProfilesAndCoins`
- ✅ `AddApplicationLogging`
- ✅ `AddPerformanceIndexes`

---

## 🚀 Deployment Steps

### Complete Deployment Command Sequence
```bash
# 1. Navigate to root directory
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning

# 2. Verify fly.toml exists
ls fly.toml

# 3. Login to Fly.io (if not already logged in)
fly auth login

# 4. Set secrets (if not already set)
fly secrets set ConnectionStrings__Default="..."
fly secrets set JwtSettings__SecretKey="..."

# 5. Deploy!
fly deploy

# 6. Open app in browser
fly open

# 7. Check logs
fly logs
```

---

## 📊 Deployment Process

### What Happens During `fly deploy`?

**Step 1: Build Docker Image**
```
==> Building image
--> Building Dockerfile
[+] Building 45.2s (12/12) FINISHED
 => [build 1/6] FROM mcr.microsoft.com/dotnet/sdk:8.0
 => [build 2/6] COPY src/MathLearning.Api/*.csproj src/MathLearning.Api/
 => [build 3/6] RUN dotnet restore
 => [build 4/6] COPY src/ src/
 => [build 5/6] RUN dotnet publish -c Release
 => [runtime] FROM mcr.microsoft.com/dotnet/aspnet:8.0
 => [runtime] COPY --from=build /app/publish .
```

**Step 2: Push Image to Registry**
```
--> Pushing image to fly.io registry
--> Image: registry.fly.io/mathlearning-api:deployment-...
```

**Step 3: Deploy to VM**
```
==> Deploying
--> v5 deployed successfully
```

**Step 4: Health Check**
```
--> Monitoring deployment
--> App is healthy!
```

---

## 🔍 Troubleshooting

### Issue 1: "Error: the config for your app is missing an app name"
**Solution**: Wrong directory. Go to root where `fly.toml` is located.
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning
```

### Issue 2: "Build failed: dotnet restore failed"
**Solution**: Check project references
```bash
# Verify projects build locally first
dotnet build src/MathLearning.Api/MathLearning.Api.csproj
```

### Issue 3: "App is not responding to health checks"
**Solution**: Check port configuration
```csharp
// Ensure Program.cs has:
app.Urls.Add("http://+:8080");
```

### Issue 4: "Database connection failed"
**Solution**: Verify connection string secret
```bash
# Check secrets
fly secrets list

# Re-set if needed
fly secrets set ConnectionStrings__Default="..."
```

### Issue 5: "Migrations not applied"
**Solution**: Apply migrations manually
```bash
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api --connection "..."
```

---

## 📋 fly.toml Configuration

```toml
app = "mathlearning-api"
primary_region = "ams"

[build]
  dockerfile = "Dockerfile"

[env]
  ASPNETCORE_URLS = "http://+:8080"

[[services]]
  internal_port = 8080
  protocol = "tcp"

  [[services.ports]]
    port = 80
    handlers = ["http"]

  [[services.ports]]
    port = 443
    handlers = ["tls", "http"]
```

**Key Settings**:
- `app = "mathlearning-api"` - App name (must match Fly.io dashboard)
- `primary_region = "ams"` - Amsterdam datacenter
- `internal_port = 8080` - Container port (matches ASPNETCORE_URLS)
- `port = 443` - HTTPS port (auto-provisioned SSL certificate)

---

## 🔒 Security Best Practices

### 1. Never Commit Secrets
```bash
# ❌ DON'T do this:
# appsettings.json
{
  "ConnectionStrings": {
    "Default": "Host=...;Password=mypassword;"  // ❌ NEVER!
  }
}

# ✅ DO this:
fly secrets set ConnectionStrings__Default="..."
```

### 2. Use Environment Variables
```csharp
// Program.cs
var connectionString = builder.Configuration.GetConnectionString("Default");
// Fly.io injects this from secrets
```

### 3. Enable HTTPS Only
```toml
# fly.toml
[[services.ports]]
  port = 443
  handlers = ["tls", "http"]
```

---

## 📊 Post-Deployment Verification

### 1. Check App Status
```bash
fly status
```

**Expected Output**:
```
App
  Name     = mathlearning-api
  Owner    = personal
  Hostname = mathlearning-api.fly.dev
  Image    = mathlearning-api:deployment-...
  Platform = machines

Machines
NAME    STATE   REGION  HEALTH CHECKS  LAST UPDATED
app     started ams     1 total        2026-01-23T15:30:00Z
```

### 2. Test API Endpoints
```bash
# Test root endpoint
curl https://mathlearning-api.fly.dev/

# Test health check
curl https://mathlearning-api.fly.dev/health

# Test login
curl -X POST https://mathlearning-api.fly.dev/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"UcimMatu!123"}'
```

### 3. Check Logs
```bash
# Real-time logs
fly logs

# Filter errors
fly logs | grep "ERR"

# Last 100 lines
fly logs -n 100
```

### 4. Monitor Resource Usage
```bash
# VM metrics
fly dashboard

# Or visit: https://fly.io/apps/mathlearning-api/metrics
```

---

## 🎯 Common Deployment Scenarios

### Scenario 1: First-Time Deployment
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning
fly launch --name mathlearning-api --region ams
fly secrets set ConnectionStrings__Default="..."
fly deploy
```

### Scenario 2: Update Deployment (Code Changes)
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning
git pull
fly deploy
```

### Scenario 3: Update Secrets Only
```bash
fly secrets set JwtSettings__SecretKey="NewSecretKey123!"
# App automatically restarts
```

### Scenario 4: Rollback to Previous Version
```bash
fly releases list
fly releases rollback v4
```

---

## 🏆 Success Indicators

After successful deployment, you should see:

✅ **Build Success**:
```
--> Image: registry.fly.io/mathlearning-api:deployment-...
--> Pushing image done
```

✅ **Deploy Success**:
```
==> Deploying
--> v5 deployed successfully
```

✅ **Health Check Pass**:
```
--> Monitoring deployment
--> App is healthy!
```

✅ **API Accessible**:
```bash
curl https://mathlearning-api.fly.dev/
# Response: "MathLearning API is running"
```

---

## 📝 Quick Reference Commands

```bash
# Deploy app
fly deploy

# Check status
fly status

# View logs
fly logs

# Open in browser
fly open

# SSH into VM
fly ssh console

# Restart app
fly apps restart mathlearning-api

# Scale VM
fly scale vm shared-cpu-1x --memory 1024

# List secrets
fly secrets list

# Set secret
fly secrets set KEY="value"

# View releases
fly releases list

# Rollback
fly releases rollback v3
```

---

## 🎓 Conclusion

**Correct Deployment Steps**:
1. ✅ Navigate to **root directory** (`cd C:\Users\...\MathLearning`)
2. ✅ Verify `fly.toml` exists (`ls fly.toml`)
3. ✅ Set secrets (`fly secrets set ...`)
4. ✅ Deploy (`fly deploy`)
5. ✅ Verify (`fly status`, `fly logs`)

**Common Mistake**: Running `fly deploy` from wrong directory (src/MathLearning.Infrastructure instead of root).

Happy deploying! 🚀
