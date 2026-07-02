# API Deployment Summary - Fly.io

## 🚀 Deployment Status
- **App Name**: mathlearning-api
- **URL**: https://mathlearning-api.fly.dev
- **Region**: Amsterdam (ams)
- **Status**: ✅ Running (2 instances)
- **Last Updated**: 2026-01-22 17:00 UTC
- **Endpoints**: ✅ Working

## ⚙️ Configuration (Secrets)

Svi konfiguracioni parametri iz `appsettings.json` su postavljeni kao Fly.io secrets:

| Parametar | Fly.io Secret Name | Status |
|-----------|-------------------|--------|
| Connection String | `ConnectionStrings__Default` | ✅ Postavljeno |
| JWT Secret Key | `JwtSettings__SecretKey` | ✅ Postavljeno |
| JWT Issuer | `JwtSettings__Issuer` | ✅ Postavljeno (MathLearningAPI) |
| JWT Audience | `JwtSettings__Audience` | ✅ Postavljeno (MathLearningApp) |
| Log Level (Default) | `Logging__LogLevel__Default` | ✅ Postavljeno (Information) |
| Log Level (ASP.NET) | `Logging__LogLevel__MicrosoftAspNetCore` | ✅ Postavljeno (Warning) |

## 📋 Komande za upravljanje

### Provera statusa
```bash
fly status -a mathlearning-api
```

### Provera secrets-a
```bash
fly secrets list -a mathlearning-api
```

### Provera logova (real-time)
```bash
fly logs -a mathlearning-api
```

### Provera logova (bez streaming-a)
```bash
fly logs -a mathlearning-api -n
```

### Restart aplikacije
```bash
fly apps restart mathlearning-api
```

### Deploy nove verzije
```bash
fly deploy -a mathlearning-api
```

## 🔐 Dodavanje/Ažuriranje Secrets-a

### Jedan secret
```bash
fly secrets set "SECRET_NAME=value" -a mathlearning-api
```

### Više secrets-a odjednom
```bash
fly secrets set "SECRET1=value1" "SECRET2=value2" -a mathlearning-api
```

### Uklanjanje secret-a
```bash
fly secrets unset SECRET_NAME -a mathlearning-api
```

## 📝 Važne napomene

1. **Automatski restart**: Kada postaviš/promeniš secret, aplikacija se automatski restartuje
2. **Format secret-a**: Koristi `__` (dva underscore) za JSON hijerarhiju
3. **Tačka u nazivu**: Ne može se koristiti tačka (`.`) u nazivu secret-a
4. **Prioritet**: Secrets imaju prioritet nad `appsettings.json`
5. **appsettings.json konflikt**: Uklonjen je appsettings.json iz Infrastructure projekta da se izbegne konflikt sa API projektom

## 🔗 Reference

- **Fly.io Dashboard**: https://fly.io/apps/mathlearning-api
- **API URL**: https://mathlearning-api.fly.dev
- **Detaljni vodič za secrets**: `FLY_IO_SECRETS_GUIDE.md`
- **Database konteksti**: `DATABASE_CONTEXTS.md`
- **Migracije vodič**: `MIGRATION_GUIDE_API.md`

## ✅ Endpoint Tests

### Root Endpoint
```bash
curl https://mathlearning-api.fly.dev/
```
**Response**: `"MathLearning API is running"`
**Status**: ✅ Working

### Auth Test Endpoint
```bash
curl https://mathlearning-api.fly.dev/auth/test
```
**Response**:
```json
{
  "message": "Auth endpoints are working!",
  "timestamp": "2026-01-22T17:01:05.5795072Z"
}
```
**Status**: ✅ Working

### Auth Login Endpoint
```bash
curl -X POST https://mathlearning-api.fly.dev/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"UcimMatu!123"}'
```
**Status**: ⏳ Pending database migration

## 🛠️ Troubleshooting

### Problem: Aplikacija ne startuje
```bash
# Proveri logove
fly logs -a mathlearning-api -n

# Proveri status
fly status -a mathlearning-api

# Restartuj
fly apps restart mathlearning-api
```

### Problem: Connection string ne radi
```bash
# Proveri da li je secret postavljen
fly secrets list -a mathlearning-api

# Ako nije, postavi ga
fly secrets set "ConnectionStrings__Default=YourConnectionString" -a mathlearning-api
```

### Problem: JWT authentication ne radi
```bash
# Proveri da li su JWT secrets postavljeni
fly secrets list -a mathlearning-api | grep JwtSettings

# Ako nisu, postavi ih
fly secrets set "JwtSettings__SecretKey=YourSecretKey" -a mathlearning-api
fly secrets set "JwtSettings__Issuer=MathLearningAPI" -a mathlearning-api
fly secrets set "JwtSettings__Audience=MathLearningApp" -a mathlearning-api
```

### Problem: Duplikat appsettings.json
**Rešenje**: Uklonjen je appsettings.json iz Infrastructure projekta. ApiDbContextFactory koristi appsettings.json iz API projekta ili environment varijable.

## ✅ Checklist za deployment

- [x] Kreiran ApiDbContext
- [x] Postavljeni svi secrets na Fly.io
- [x] Aplikacija deployovana i radi
- [x] Connection string konfigurisan
- [x] JWT authentication konfigurisan
- [x] Logging konfigurisan
- [x] Duplikat appsettings.json rešen
- [x] API endpoints testirani i rade
- [x] Auth endpoints testirani i rade
- [ ] Database migracije primenjene (sledeći korak)
- [ ] Admin user kreiran u bazi

## 🔜 Sledeći koraci

1. **Primeni database migracije na production bazu**:
   ```bash
   # Lokalno sa connection string-om
   cd src/MathLearning.Infrastructure
   dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api --connection "YourProductionConnectionString"
   ```

2. **Testiraj autentifikaciju sa admin userom**:
   ```bash
   curl -X POST https://mathlearning-api.fly.dev/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"UcimMatu!123"}'
   ```

3. **Testiraj protected endpoints sa JWT tokenom**:
   ```bash
   TOKEN="your-jwt-token"
   curl https://mathlearning-api.fly.dev/api/progress/overview \
     -H "Authorization: Bearer $TOKEN"
   ```

## 🎉 Deployment Complete!

API je uspešno deploy-ovan na Fly.io sa svim potrebnim konfiguracijama:
- ✅ ApiDbContext kreiran i konfigurisan
- ✅ Environment varijable postavljene
- ✅ JWT authentication konfigurisan
- ✅ Endpoints testirani i funkcionalni
- ✅ Build i deployment uspešan

**Next**: Primeni migracije na production bazu i testiraj sa pravim podacima.
