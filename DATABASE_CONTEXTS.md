# Database Context Architecture

Ovaj projekat koristi tri odvojena DbContext-a za različite potrebe:

## 1. ApiDbContext
- **Lokacija**: `MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- **Projekat**: `MathLearning.Api`
- **Migracije**: `MathLearning.Infrastructure/Migrations/Api/`
- **Connection String**: `Default` (iz appsettings.json)
- **Sadržaj**:
  - ASP.NET Core Identity tabele (AspNetUsers, AspNetRoles, itd.)
  - Svi domenski entiteti (Questions, Topics, Subtopics, QuizSessions, UserAnswers, UserQuestionStats, UserFriends)

**Namena**: Kompletna baza podataka za API projekat koji koristi Identity za autentifikaciju i sve funkcionalnosti kviz aplikacije.

## 2. AdminDbContext
- **Lokacija**: `MathLearning.Admin/Data/AdminDbContext.cs`
- **Projekat**: `MathLearning.Admin` (Blazor)
- **Migracije**: `MathLearning.Admin/Migrations/`
- **Connection String**: `AdminIdentity` (iz appsettings.json)
- **Sadržaj**:
  - ASP.NET Core Identity tabele
  - Osnovni entiteti (Questions, QuestionOptions, Categories)

**Namena**: Admin panel sa Identity autentifikacijom i CRUD operacijama za pitanja i kategorije.

## 3. AppDbContext
- **Lokacija**: `MathLearning.Infrastructure/Persistance/AppDbContext.cs`
- **Projekat**: Legacy (trenutno se više ne koristi aktivno)
- **Migracije**: `MathLearning.Infrastructure/Migrations/`
- **Connection String**: `Default` (iz appsettings.json)
- **Sadržaj**: Svi domenski entiteti (bez Identity)

**Namena**: Prvobitni DbContext pre razdvajanja na API i Admin kontekste.

## Kreiranje Migracija

### Za ApiDbContext:
```bash
cd src/MathLearning.Infrastructure
dotnet ef migrations add MigrationName --context ApiDbContext --output-dir Migrations/Api
dotnet ef database update --context ApiDbContext --project ../MathLearning.Api/MathLearning.Api.csproj
```

### Za AdminDbContext:
```bash
cd src/MathLearning.Admin
dotnet ef migrations add MigrationName --context AdminDbContext
dotnet ef database update --context AdminDbContext
```

### Za AppDbContext (legacy):
```bash
cd src/MathLearning.Infrastructure
dotnet ef migrations add MigrationName --context AppDbContext
dotnet ef database update --context AppDbContext --project ../MathLearning.Api/MathLearning.Api.csproj
```

## Napomene

- **ApiDbContext** i **AdminDbContext** dele istu bazu (Default connection string) ali imaju različite migracije
- Ako treba sinhronizovati šemu između njih, potrebno je ručno kreirati migracije za oba konteksta
- **AppDbContext** se može ukloniti u budućnosti kada se potvrdi da sve radi sa novim strukturom
