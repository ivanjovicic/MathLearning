# Deploy MathLearning.Admin to Render

Ovaj dokument sadrži korake i preporuke za deploy `MathLearning.Admin` (Server‑Side Blazor) na Render.

## Kratki pregled
- `MathLearning.Admin` je Server‑Side Blazor aplikacija i treba da bude deployovana kao zaseban Web Service.
- Za Render je najstabilnija varijanta Docker-based deploy preko Blueprint-a (`runtime: docker`).
- Aplikacija na startu pokušava da izvrši EF migracije i (opciono) seed podataka — obezbedite validan DB connection string kao env var / tajnu.
- Data Protection ključevi se čuvaju u `/app/keys` u kodu; za production koristite Persistent Disk ili deljeno skladište.

---

## Preporučeni Blueprint (`render.yaml`)

```yaml
services:
	- type: web
		name: mathlearning-admin
		runtime: docker
		branch: main
		dockerfilePath: ./src/MathLearning.Admin/Dockerfile
		dockerContext: .
		healthCheckPath: /healthz
		envVars:
			- key: ASPNETCORE_ENVIRONMENT
				value: Production
			- key: Database__InitializeOnStartup
				value: "false"
			- key: SeedAdmin__Enabled
				value: "false"
			- key: SeedContent__Enabled
				value: "false"
			- key: ConnectionStrings__AdminIdentity
				sync: false
```

Admin sada ima poseban Dockerfile u `src/MathLearning.Admin/Dockerfile`, a API koristi root `Dockerfile`.

---

## Obavezne environment varijable / Secrets
U Blueprint-u koristi `envVars`, a za osetljive vrednosti koristi `sync: false` ili ih ručno unesi u Render Dashboard za postojeće servise.

- `ConnectionStrings__AdminIdentity` — vrednost u key=value formatu (primer za Neon):

```
Host=ep-spring-glade-agaxudii-pooler.c-2.eu-central-1.aws.neon.tech;Port=5432;Username=neondb_owner;Password=<tvoja_lozinka>;Database=neondb;Ssl Mode=Require
```

- `ASPNETCORE_ENVIRONMENT` = `Production`
- `Database__InitializeOnStartup` = `false` na web servisu. U production-u uključi `true` samo ako namerno želiš da admin web proces izvršava migracije i seed pre prihvatanja saobraćaja.
- Opcionalno: `SeedAdmin__Enabled` = `true|false` (ako želiš da seed/admin user bude kreiran na prvom startu)
- Opcionalno: `SeedContent__Enabled` = `true|false`

Napomena: `sync: false` te pita za vrednost samo tokom inicijalnog kreiranja Blueprint-a. Ako servis već postoji, dodaj/izmeni ove env varijable ručno u Render Dashboard-u.

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
1. New -> Blueprint ili New -> Web Service -> poveži GitHub/Git repo.
2. Izaberi branch (npr. `main`) i potvrdi da Render čita `render.yaml` iz repo root-a.
3. Za `mathlearning-admin` Render koristi `runtime: docker`, `dockerfilePath: ./src/MathLearning.Admin/Dockerfile` i `dockerContext: .`.
4. Tokom inicijalnog Blueprint setup-a unesi vrednost za `ConnectionStrings__AdminIdentity` kada Render traži `sync: false` env var.
5. Dodaj Persistent Disk i mount path `/app/keys` (ako koristiš više instanci ili želiš da sačuvaš ključeve preko restarta).
6. Deploy i gledaj Logs (Dashboard -> Logs) za poruke o migracijama / greškama.

---

## Tipične greške i rešavanje
- `Couldn't set postgresql://...` — znači da je prosleđen URI u mestu gde se očekuje key=value; postavi `ConnectionStrings__AdminIdentity` kao key=value.
- `Host can't be null` — connection string nije postavljen ili nije dostupan procesu (proveri da li si dodao tajnu kao Secret i da li je ime varijable tačno).
- Health check vraća `404` sa `x-render-routing: no-server` — Render nema zdravu instancu iza hosta. Najčešći uzrok je da se web proces blokira na startup DB migracijama ili da deploy nije uspeo. Ostavite `Database__InitializeOnStartup=false` i migracije pokrenite odvojeno.
- `The WebRootPath was not found` ili statični fajlovi nedostupni — ovo je rešeno Docker pristupom, jer se aplikacija startuje iz publish output-a unutar image-a.
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
