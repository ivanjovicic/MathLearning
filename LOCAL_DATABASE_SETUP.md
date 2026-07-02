# Local Database Setup

Uputstvo za podešavanje lokalne PostgreSQL baze umesto Neon cloud baze.

## Preduslov

Instaliraj **Docker Desktop** na svom računaru:
- Windows/Mac: https://www.docker.com/products/docker-desktop
- Linux: `sudo apt-get install docker-compose`

---

## Brzi Start

### 1️⃣ Pokreni PostgreSQL kontejner

```powershell
# Windows PowerShell
.\scripts\setup-local-db.ps1
```

Ili ručno:

```bash
# Pokreni Docker Compose
docker-compose up -d

# Proveri status
docker ps

# Logovi
docker logs mathlearning-postgres
```

### 2️⃣ Primeni EF Core migracije

```powershell
# Admin baza
cd src/MathLearning.Admin
dotnet ef database update --context AdminDbContext

# API baza
cd ../MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api/MathLearning.Api.csproj
```

### 3️⃣ Pokreni aplikaciju

```powershell
# API
cd src/MathLearning.Api
dotnet run

# Admin
cd src/MathLearning.Admin
dotnet run
```

---

## Connection String Konfiguracija

### appsettings.json (već ažurirano)

**API** (`src/MathLearning.Api/appsettings.json`):
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=mathlearning;Pooling=true;"
  }
}
```

**Admin** (`src/MathLearning.Admin/appsettings.json`):
```json
{
  "ConnectionStrings": {
    "AdminIdentity": "Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=mathlearning_admin;Pooling=true;"
  }
}
```

---

## Database Info

| Parameter | Value |
|-----------|-------|
| Host | `localhost` |
| Port | `5433` (mapped from container port 5432) |
| Username | `postgres` |
| Password | `postgres` |
| Databases | `mathlearning`, `mathlearning_admin` |

---

## Docker Commands

### Zaustavi bazu
```bash
docker-compose down
```

### Zaustavi i obriši podatke
```bash
docker-compose down -v
```

### Restart
```bash
docker-compose restart
```

### Logovi
```bash
docker logs -f mathlearning-postgres
```

### Pristupi PostgreSQL CLI
```bash
docker exec -it mathlearning-postgres psql -U postgres -d mathlearning
```

---

## pgAdmin / DBeaver Setup

### pgAdmin
1. Otvori pgAdmin
2. Add New Server:
   - Name: `MathLearning Local`
   - Host: `localhost`
   - Port: `5433`
   - Username: `postgres`
   - Password: `postgres`

### DBeaver
1. New Database Connection → PostgreSQL
2. Host: `localhost`
3. Port: `5433`
4. Database: `mathlearning`
5. Username: `postgres`
6. Password: `postgres`

---

## Troubleshooting

### Port 5433 already in use
```bash
# Pronađi proces koji koristi port
netstat -ano | findstr :5433

# Zaustavi Docker kontejner
docker-compose down

# Promeni port u docker-compose.yml (npr. 5434)
# Onda ažuriraj appsettings.json connection strings
```

### Container won't start
```bash
# Proveri logove
docker logs mathlearning-postgres

# Pokušaj rebuild
docker-compose down -v
docker-compose up -d --build
```

### Migration failed
```bash
# Proveri da li je baza dostupna
docker exec -it mathlearning-postgres psql -U postgres -c "SELECT version();"

# Ručno kreiraj bazu ako ne postoji
docker exec -it mathlearning-postgres psql -U postgres -c "CREATE DATABASE mathlearning;"
docker exec -it mathlearning-postgres psql -U postgres -c "CREATE DATABASE mathlearning_admin;"

# Pokušaj ponovo migration
dotnet ef database update --context ApiDbContext
```

### Cannot connect from application
- Proveri da li je Docker Desktop pokrenut
- Proveri da li kontejner radi: `docker ps`
- Proveri port mapping: `docker port mathlearning-postgres`
- Proveri connection string u `appsettings.json`

---

## Backup & Restore

### Backup
```bash
# Backup mathlearning baze
docker exec mathlearning-postgres pg_dump -U postgres mathlearning > backup_$(date +%Y%m%d).sql

# Backup admin baze
docker exec mathlearning-postgres pg_dump -U postgres mathlearning_admin > backup_admin_$(date +%Y%m%d).sql
```

### Restore
```bash
# Restore mathlearning baze
docker exec -i mathlearning-postgres psql -U postgres mathlearning < backup_20260207.sql

# Restore admin baze
docker exec -i mathlearning-postgres psql -U postgres mathlearning_admin < backup_admin_20260207.sql
```

---

## Razlike: Neon vs Lokalna Baza

| Feature | Neon Cloud | Lokalna (Docker) |
|---------|------------|------------------|
| Setup | Online account | Docker Desktop |
| Cost | Free tier / Paid | Free |
| Performance | Network latency | Local (brže) |
| Availability | 99.9% SLA | Zavisi od Docker-a |
| Backups | Automatic | Manual |
| SSL | Required | Ne treba |
| Scaling | Automatic | Manual |
| Development | Sporo (network) | Brzo |
| Production | ✅ Preporučeno | ❌ Ne preporučuje se |

---

## Preporuka za Production

Za **production**, nastavi da koristiš **Neon** ili prebaci na:
- **Azure Database for PostgreSQL**
- **AWS RDS PostgreSQL**
- **Google Cloud SQL**

Lokalna baza je **samo za development**!

---

## Seed Data (Opcionalno)

Ako želiš da seeduješ testne podatke:

```powershell
# Pokreni seed script (ako postoji)
cd src/MathLearning.Infrastructure
dotnet run --project ../MathLearning.Api -- --seed

# Ili ručno izvršavaj SQL
docker exec -i mathlearning-postgres psql -U postgres mathlearning < seed.sql
```
