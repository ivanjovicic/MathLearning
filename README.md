# MathLearning

MathLearning is a learning platform for mathematics that combines a scalable .NET backend with a cross-platform Flutter mobile app (iOS & Android). The project supports quizzes, progress tracking, leaderboards, localization and offline usage.

## Project overview

- Backend: ASP.NET Core web API providing REST endpoints, business logic and data access.
- Mobile frontend: Flutter application for students to take quizzes, view progress and compete on leaderboards.
- Data stores: PostgreSQL as the primary relational database, Redis used for caching and fast leaderboard access.
- Jobs & services: background translation jobs, migration scripts, and optional hosted deployments (Docker / Fly.io).

## Key features

- Dynamic quizzes with multiple question types (MCQ, open answer)
- User profiles, progress statistics and history
- Real-time / near-real-time leaderboard backed by Redis
- Offline support with local caching and later synchronization
- Internationalization (translation job) and multi-language support
- Token-based authentication with refresh tokens

## Tech stack

- Backend: .NET 6+/ASP.NET Core, Clean/Layered architecture
- ORM: Entity Framework Core
- Database: PostgreSQL
- Cache / Leaderboard: Redis
- Frontend: Flutter (Dart)
- Containerization: Docker, docker-compose
- CI/CD: configured via project pipelines (examples in repo)

## Local development

1. Start the local database (PostgreSQL) and Redis. You can use docker-compose or the provided scripts under `scripts/`.

PowerShell example (from repository root):

```powershell
.\scripts
aively-run-local-db.ps1  # replace with actual script names like start-local-db-and-migrate.ps1
```

2. Apply EF Core migrations and run the API (from `src/MathLearning.Api`):

```powershell
dotnet restore
dotnet build
dotnet run --project src\MathLearning.Api\MathLearning.Api.csproj
```

3. Run the Flutter app (from `src/` where the Flutter project lives):

```bash
flutter pub get
flutter run
```

Note: adjust paths if your Flutter project is in a different folder.

## Useful folders

- `src/MathLearning.Api` — backend API project
- `src/MathLearning.Application` — application/business logic
- `src/MathLearning.Infrastructure` — persistence, caching, external integrations
- `src/MathLearning.TranslationJob` — background translation job
- `scripts/` — helper scripts for local setup and migrations

## Tests

Unit and integration tests live under `tests/MathLearning.Tests`. Run them with:

```powershell
dotnet test
```

## Contributing

Contributions are welcome. Please open issues or pull requests and follow the established branch/migration conventions in the repo.

## Contact

For questions about the project or to request access, contact the maintainers listed in the repository.

