# Bug Reporting System

Sistem za prijavljivanje i upravljanje bagovima prijavljenim iz mobilne aplikacije.

## API Endpoints

### Public Endpoints (Authenticated Users)

#### POST /api/bugs/report
Prijavi bug iz frontend aplikacije.

**Request:**
```json
{
  "screen": "QuizScreen",
  "description": "App crashes when submitting answer",
  "stepsToReproduce": "1. Start quiz\n2. Answer question\n3. Tap submit",
  "severity": "high",
  "platform": "Android",
  "locale": "sr",
  "appVersion": "1.0.0",
  "screenshotBase64": "data:image/png;base64,iVBORw0KGgo..."
}
```

**Severity values:** `low`, `medium`, `high`, `critical`

**Response:**
```json
{
  "id": "uuid",
  "createdAt": "2026-02-07T12:00:00Z",
  "userId": 123,
  "usernameSnapshot": "john_doe",
  "screen": "QuizScreen",
  "description": "App crashes when submitting answer",
  "stepsToReproduce": "...",
  "severity": "high",
  "platform": "Android",
  "locale": "sr",
  "appVersion": "1.0.0",
  "screenshotUrl": "/uploads/screenshots/123_abc123.png",
  "status": "open",
  "resolvedAt": null,
  "assignee": null
}
```

#### GET /api/bugs/mine?page=1&pageSize=50
Lista bugova koje je prijavio trenutni korisnik.

**Response:**
```json
{
  "bugs": [ ... ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 50
}
```

### Admin Endpoints (Authorization Required)

#### GET /api/bugs?page=1&pageSize=20&status=open&severity=high
Lista svih bugova sa paging i filterima.

**Query Parameters:**
- `page` (default: 1)
- `pageSize` (default: 20, max: 100)
- `status` (optional): `open`, `in_progress`, `fixed`, `closed`
- `severity` (optional): `low`, `medium`, `high`, `critical`

#### GET /api/bugs/{id}
Preuzmi pojedinačan bug report.

#### PATCH /api/bugs/{id}
Promeni status buga.

**Request:**
```json
{
  "status": "in_progress",
  "assignee": "admin@example.com"
}
```

**Status values:** `open`, `in_progress`, `fixed`, `closed`

---

## Admin UI

### Blazor stranica: `/bugs`

Features:
- **Listing** sa paging-om (20 po strani)
- **Filtering** po status-u i severity-u
- **Color coding**: 
  - Severity: critical=red, high=orange, medium=blue, low=default
  - Status: open=red, in_progress=blue, fixed=green, closed=default
- **Actions**: View details, Update status

---

## Screenshot Storage

### Trenutno: Local File Storage
- Lokacija: `uploads/screenshots/`
- Max veličina: 5MB
- Format: PNG, JPEG
- URL format: `/uploads/screenshots/{userId}_{guid}.png`

### Preporuka: Object Storage (budućnost)
Za production okruženje, preporučuje se prebacivanje na cloud object storage:
- **Azure Blob Storage**
- **AWS S3**
- **Cloudinary** (ima image optimization)

**Prednosti:**
- Neograničen storage
- CDN integration
- Automatska optimizacija slika
- Bolje performanse

**Implementacija:**
Kreiraj `CloudScreenshotStorageService : IScreenshotStorageService` sa upload metodom koja vraća public URL.

---

## Database Schema

```sql
CREATE TABLE bug_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    user_id INT NOT NULL,
    username_snapshot VARCHAR(256) NOT NULL,
    screen VARCHAR(100) NOT NULL,
    description VARCHAR(2000) NOT NULL,
    steps_to_reproduce VARCHAR(2000),
    severity VARCHAR(20) NOT NULL,
    platform VARCHAR(50) NOT NULL,
    locale VARCHAR(10) NOT NULL,
    app_version VARCHAR(20) NOT NULL,
    screenshot_url VARCHAR(500),
    status VARCHAR(20) NOT NULL DEFAULT 'open',
    resolved_at TIMESTAMP,
    assignee VARCHAR(256)
);

CREATE INDEX ix_bug_reports_user_id ON bug_reports(user_id);
CREATE INDEX ix_bug_reports_created_at ON bug_reports(created_at);
CREATE INDEX ix_bug_reports_status ON bug_reports(status);
CREATE INDEX ix_bug_reports_severity ON bug_reports(severity);
CREATE INDEX ix_bug_reports_status_created_at ON bug_reports(status, created_at);
CREATE INDEX ix_bug_reports_user_created_at ON bug_reports(user_id, created_at);
```

---

## Frontend Integration (Flutter)

### Model
```dart
class BugReport {
  final String id;
  final DateTime createdAt;
  final String screen;
  final String description;
  final String? stepsToReproduce;
  final String severity;
  final String status;
  final String? screenshotUrl;

  BugReport.fromJson(Map<String, dynamic> json)
      : id = json['id'],
        createdAt = DateTime.parse(json['createdAt']),
        screen = json['screen'],
        description = json['description'],
        stepsToReproduce = json['stepsToReproduce'],
        severity = json['severity'],
        status = json['status'],
        screenshotUrl = json['screenshotUrl'];
}
```

### Service
```dart
class BugReportService {
  final ApiClient _api;

  Future<void> reportBug({
    required String screen,
    required String description,
    String? stepsToReproduce,
    required String severity,
    String? screenshotBase64,
  }) async {
    await _api.post('/bugs/report', {
      'screen': screen,
      'description': description,
      'stepsToReproduce': stepsToReproduce,
      'severity': severity,
      'platform': Platform.isAndroid ? 'Android' : 'iOS',
      'locale': 'sr',
      'appVersion': '1.0.0',
      'screenshotBase64': screenshotBase64,
    });
  }

  Future<List<BugReport>> getMyBugs({int page = 1, int pageSize = 50}) async {
    final response = await _api.get('/bugs/mine?page=$page&pageSize=$pageSize');
    return (response['bugs'] as List)
        .map((json) => BugReport.fromJson(json))
        .toList();
  }
}
```

---

## Migracija

EF Core migracija je kreirana:

```bash
dotnet ef migrations add AddBugReports --context ApiDbContext --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api
```

Primeni migraciju:

```bash
dotnet ef database update --context ApiDbContext --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api
```

---

## Testing

### Testiranje endpointa (curl)

```bash
# Report bug
curl -X POST https://localhost:7001/api/bugs/report \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "screen": "QuizScreen",
    "description": "Test bug",
    "severity": "medium",
    "platform": "Android",
    "locale": "sr",
    "appVersion": "1.0.0"
  }'

# Get my bugs
curl https://localhost:7001/api/bugs/mine \
  -H "Authorization: Bearer {token}"

# Admin: Get all bugs
curl "https://localhost:7001/api/bugs?status=open&severity=high" \
  -H "Authorization: Bearer {token}"

# Admin: Update status
curl -X PATCH https://localhost:7001/api/bugs/{id} \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{
    "status": "in_progress",
    "assignee": "admin@example.com"
  }'
```

---

## TODO / Future Improvements

- [ ] **Email notifications** kad je bug resolved
- [ ] **Webhook** za Slack/Discord notifikacije
- [ ] **Duplicate detection** (similar bugs)
- [ ] **Bug comments/notes** sistem
- [ ] **Attachments** (multiple screenshots)
- [ ] **Cloud storage** za screenshots
- [ ] **Analytics dashboard** (bugs by screen, severity distribution)
- [ ] **Export to CSV** funkcionalnost
- [ ] **Bulk status update**
- [ ] **Bug voting** (users can upvote)
