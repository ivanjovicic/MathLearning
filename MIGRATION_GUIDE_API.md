# Kreiranje migracija za ApiDbContext

## Koraci za kreiranje i primenu migracija

### 1. Kreiranje nove migracije

```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef migrations add NazivMigracije --context ApiDbContext --output-dir Migrations/Api
```

### 2. Primena migracija na bazu podataka

Postoje dva načina:

#### Način 1: Iz Infrastructure projekta (direktno)
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext
```

#### Način 2: Iz Infrastructure projekta uz specifikaciju startup projekta
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api/MathLearning.Api.csproj
```

### 3. Pregled migracija

```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef migrations list --context ApiDbContext
```

### 4. Rollback migracije

```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef database update PrethodnaMigracija --context ApiDbContext
```

### 5. Uklanjanje poslednje migracije (ako nije primenjena)

```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning\src\MathLearning.Infrastructure
dotnet ef migrations remove --context ApiDbContext
```

## Napomene

- **ApiDbContext** sadrži sve Identity tabele (AspNetUsers, AspNetRoles, itd.) i sve domenске entitete
- Connection string se nalazi u `MathLearning.Api/appsettings.json` pod ključem `"Default"`
- Za razliku od AppDbContext-a, ApiDbContext je nasledjen od `IdentityDbContext<IdentityUser>`
- Migracije se čuvaju u folderu `Migrations/Api` da bi bile odvojene od ostalih migracija

## Rešavanje problema

### Problem: "Unable to create an object of type 'ApiDbContext'"
**Rešenje**: Proveri da li postoji `appsettings.json` u `MathLearning.Infrastructure` folderu sa validnim connection string-om.

### Problem: "Build failed"
**Rešenje**: Prvo izgradi projekat:
```bash
cd C:\Users\Alex\source\repos\Mathlearning\MathLearning
dotnet build
```

### Problem: "The context type is not configured as a service"
**Rešenje**: Koristi `--startup-project` parametar da specificiraš API projekat kao startup projekat.
