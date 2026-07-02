# Fly.io Secrets Management Guide

## Trenutni Secrets na mathlearning-api

| Secret Name | Opis |
|------------|------|
| `ConnectionStrings__Default` | PostgreSQL connection string za Neon bazu |
| `JwtSettings__SecretKey` | Secret key za JWT token enkripciju |
| `JwtSettings__Issuer` | JWT token issuer |
| `JwtSettings__Audience` | JWT token audience |
| `Logging__LogLevel__Default` | Default logging nivo (Information) |
| `Logging__LogLevel__MicrosoftAspNetCore` | ASP.NET Core logging nivo (Warning) |

## Komande za upravljanje secrets-ima

### Prikaz svih secrets-a
```bash
fly secrets list -a mathlearning-api
```

### Dodavanje/Ažuriranje jednog secret-a
```bash
fly secrets set "SECRET_NAME=value" -a mathlearning-api
```

### Dodavanje/Ažuriranje više secrets-a odjednom
```bash
fly secrets set "SECRET1=value1" "SECRET2=value2" -a mathlearning-api
```

### Brisanje secret-a
```bash
fly secrets unset SECRET_NAME -a mathlearning-api
```

## Mapiranje appsettings.json na Fly.io Secrets

ASP.NET Core koristi `__` (dva underscore) za navigaciju kroz hijerarhiju JSON konfiguracije.

### Primer 1: Connection String
**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "Default": "Host=..."
  }
}
```

**Fly.io secret:**
```bash
fly secrets set "ConnectionStrings__Default=Host=..." -a mathlearning-api
```

### Primer 2: JWT Settings
**appsettings.json:**
```json
{
  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "...",
    "Audience": "..."
  }
}
```

**Fly.io secrets:**
```bash
fly secrets set "JwtSettings__SecretKey=..." -a mathlearning-api
fly secrets set "JwtSettings__Issuer=..." -a mathlearning-api
fly secrets set "JwtSettings__Audience=..." -a mathlearning-api
```

### Primer 3: Logging Configuration
**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Fly.io secrets (napomena: tačka se ne može koristiti u nazivu):**
```bash
fly secrets set "Logging__LogLevel__Default=Information" -a mathlearning-api
fly secrets set "Logging__LogLevel__MicrosoftAspNetCore=Warning" -a mathlearning-api
```

## Važne napomene

1. **Automatsko ponovno pokretanje**: Kada postaviš secret, Fly.io automatski restartuje sve instance aplikacije

2. **Escape specijalni karakteri**: Ako vrednost sadrži specijalne karaktere, stavi je u navodnike:
   ```bash
   fly secrets set "KEY=value with spaces" -a mathlearning-api
   ```

3. **Tačka u nazivu**: Fly.io ne dozvoljava tačku (`.`) u nazivu secret-a. Umesto `Microsoft.AspNetCore` koristi `MicrosoftAspNetCore`

4. **Prioritet konfiguracije**: Environment varijable/Secrets imaju prioritet nad appsettings.json

5. **Sigurnost**: Nikada ne commituj secrets u Git! Koristi Fly.io secrets ili environment varijable

## Sinhronizacija sa appsettings.json

Kada dodaješ novi setting u `appsettings.json`, ne zaboravi da ga dodaš i na Fly.io:

1. Dodaj setting u `src/MathLearning.Api/appsettings.json`
2. Pretvori JSON putanju u Fly.io format (zameni `.` sa `__`)
3. Postavi secret na Fly.io
4. Deploy aplikaciju

## Primer kompletne sinhronizacije

```bash
# 1. Pročitaj trenutne secrets
fly secrets list -a mathlearning-api

# 2. Dodaj nove secrets ako postoje novi u appsettings.json
fly secrets set "NewSetting__Key=value" -a mathlearning-api

# 3. Deploy aplikaciju
fly deploy -a mathlearning-api
```

## Troubleshooting

### Problem: Secret nije prepoznat
**Rešenje**: Proveri da li koristiš `__` (dva underscore) za hijerarhiju

### Problem: Aplikacija ne vidi secret
**Rešenje**: Restartuj aplikaciju:
```bash
fly apps restart mathlearning-api
```

### Problem: Ne mogu da settujem secret sa tačkom
**Rešenje**: Ukloni tačku iz naziva:
- ❌ `Microsoft.AspNetCore`
- ✅ `MicrosoftAspNetCore`
