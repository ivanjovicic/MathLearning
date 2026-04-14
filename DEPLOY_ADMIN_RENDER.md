# Deploy MathLearning.Admin to Render

Ovaj dokument sadrži korake i preporuke za deploy `MathLearning.Admin` (Server‑Side Blazor) na Render.

## Kratki pregled
- `MathLearning.Admin` je Server‑Side Blazor aplikacija i treba da bude deployovana kao zaseban Web Service.
- Aplikacija na startu pokušava da izvrši EF migracije i (opciono) seed podataka — obezbedite validan DB connection string kao tajnu.
- Data Protection ključevi se čuvaju u `/app/keys` u kodu; za production koristite Persistent Disk ili deljeno skladište.

---

## Preporučene komande (Render Web Service)
- **Build command** (Render → Build Command):

```bash
dotnet publish src/MathLearning.Admin -c Release -o published
```

- **Start command** (Render → Start Command):

```bash
mkdir -p /app/keys && dotnet published/MathLearning.Admin.dll --urls http://0.0.0.0:$PORT
```

> Napomena: `mkdir -p` i `&&` su bash sintaksa i ispravni su u Render okruženju (Linux). Nemoj koristiti ovu liniju direktno u PowerShell 5.1 (koristi `New-Item` i `;` u PowerShell-u lokalno).

---

## Obavezne environment varijable / Secrets
U Render dashboardu dodaj `Environment Variable` ili (poželjno) `Secret` sa sledećim ključevima:

- `ConnectionStrings__AdminIdentity` — vrednost u key=value formatu (primer za Neon):

```
Host=ep-spring-glade-agaxudii-pooler.c-2.eu-central-1.aws.neon.tech;Port=5432;Username=neondb_owner;Password=<tvoja_lozinka>;Database=neondb;Ssl Mode=Require
```

- `ASPNETCORE_ENVIRONMENT` = `Production`
- Opcionalno: `SeedAdmin__Enabled` = `true|false` (ako želiš da seed/admin user bude kreiran na prvom startu)
- Opcionalno: `SeedContent__Enabled` = `true|false`

**Važno:** aplikacija očekuje key=value connection string; izbegavaj prosleđivanje prefiksa `psql 'postgresql://...'`. Ako koristite URI formu i dobijate greške, prebacite se na key=value format.

---

## Persistent Data Protection Keys
Server‑Side Blazor + Identity koristi DataProtection ključeve (u kodu su podešeni da se čuvaju u `/app/keys`). Ako pokrećete više instanci, morate obezbediti deljeni storage za te ključeve.

Preporuka na Render:
1. U Service settings -> Disks -> `Add Persistent Disk`.
2. Mount Path: `/app/keys`.
3. Deploy.

Alternativa: koristiti eksterni key store (Azure Blob, Redis, ili DB) ako ne želite persistent disk.

---

## Health check
- Postavi Health Check Path na `/healthz` (Render → Health Checks). Aplikacija vraća jednostavan JSON na tom endpointu.

---

## Koraci u Render UI (sažeto)
1. New -> Web Service -> poveži GitHub/Git repo.
2. Izaberi branch (npr. `main`).
3. Build Command: `dotnet publish src/MathLearning.Admin -c Release -o published`
4. Start Command: `mkdir -p /app/keys && dotnet published/MathLearning.Admin.dll --urls http://0.0.0.0:$PORT`
5. Dodaj Secret: `ConnectionStrings__AdminIdentity` (key=value string).
6. Dodaj Persistent Disk i mount path `/app/keys` (ako koristiš više instanci ili želiš da sačuvaš ključeve preko restarta).
7. Deploy i gledaj Logs (Dashboard -> Logs) za poruke o migracijama / greškama.

---

## Tipične greške i rešavanje
- `Couldn't set postgresql://...` — znači da je prosleđen URI u mestu gde se očekuje key=value; postavi `ConnectionStrings__AdminIdentity` kao key=value.
- `Host can't be null` — connection string nije postavljen ili nije dostupan procesu (proveri da li si dodao tajnu kao Secret i da li je ime varijable tačno).
- `The WebRootPath was not found` ili statični fajlovi nedostupni — proveri da li si koristio `dotnet publish` kao build step i startuješ iz `published` foldera (start komanda gore radi to).
- Problemi sa pristupom bazi: proveri da li Neon/DB dozvoljava konekcije sa Render (networking/allowlist), kao i `Ssl Mode` vrednost.

---

## Primer: lokalno testiranje pre deploy-a (PowerShell)
```powershell
# postavi connection string u sadašnjoj PowerShell sesiji (prime zbog & u URI koristite single quotes)
$Env:ConnectionStrings__AdminIdentity='Host=ep-spring-glade-agaxudii-pooler.c-2.eu-central-1.aws.neon.tech;Port=5432;Username=neondb_owner;Password=<tvoja_lozinka>;Database=neondb;Ssl Mode=Require'

# kreiraj keys folder
New-Item -ItemType Directory -Force -Path .\app\keys

# pokreni aplikaciju iz published foldera
Set-Location .\published
$Env:PORT=5002
dotnet .\MathLearning.Admin.dll --urls "http://0.0.0.0:$Env:PORT"
```

---

## Želiš li da pripremim `render.yaml` ili automatizovan start-script?
Mogu da dodam primer `render.yaml` ili `start.sh` u repo ako želiš da ga povežeš automatski sa Render-om. Takođe mogu pripremiti gotov tekst za Render Secret (bez lozinki) koji možeš copy/paste-ovati u Dashboard.

---

Lokacija fajla: [DEPLOY_ADMIN_RENDER.md](DEPLOY_ADMIN_RENDER.md)
