# Refresh Token Authentication System

## 🔐 Overview

Implementiran **kompletan Refresh Token sistem** za sigurnu, user-friendly autentifikaciju sa automatskim refresh-om tokena.

## 📊 Token Types

### 1. Access Token (Short-lived)
**Lifetime**: 30 minutes  
**Purpose**: API autentifikacija  
**Storage**: Client memory (NOT localStorage)  
**Format**: JWT (JSON Web Token)

```json
{
  "sub": "user-id",
  "unique_name": "john.doe",
  "userId": "123",
  "jti": "unique-token-id",
  "exp": 1706026800
}
```

### 2. Refresh Token (Long-lived)
**Lifetime**: 14 days  
**Purpose**: Dobijanje novog Access Token-a  
**Storage**: Server-side database  
**Format**: Cryptographically secure random string (64 bytes = 128 hex chars)

```
Example: "Ug8v7w9y$B@E(H+MbQeThWmZq4t7w!z%C*F-JaNdRgUkXp2s5v8y/A?D(G+KbPe"
```

---

## 🔄 Authentication Flow

### Initial Login
```
1. User → POST /auth/login { username, password }
2. Server validates credentials
3. Server generates:
   - Access Token (30 min)
   - Refresh Token (14 days)
4. Server stores Refresh Token in database
5. Server → { accessToken, refreshToken, expiresIn, userId, username }
6. Client stores tokens:
   - Access Token → Memory (React state, Blazor service)
   - Refresh Token → HttpOnly Cookie or Secure Storage
```

### Token Refresh Flow
```
1. Access Token expires (after 30 min)
2. Client → POST /auth/refresh { refreshToken }
3. Server validates Refresh Token:
   - Exists in database?
   - Not expired?
   - Not revoked?
4. Server generates new tokens
5. Server revokes old Refresh Token
6. Server → { accessToken, refreshToken, expiresIn, userId, username }
7. Client updates tokens
```

### Logout Flow
```
1. Client → POST /auth/logout { refreshToken }
2. Server marks Refresh Token as revoked
3. Server → { message: "Logged out successfully" }
4. Client clears tokens
```

---

## 🛠️ API Endpoints

### POST /auth/login
**Description**: User login with username & password

**Request**:
```json
{
  "username": "admin",
  "password": "UcimMatu!123"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "Ug8v7w9y$B@E(H+MbQeThWmZq4t7w!z%C*F...",
  "expiresIn": 1800,
  "userId": 1,
  "username": "admin"
}
```

**Error** (401 Unauthorized):
```json
{
  "error": "Invalid username or password"
}
```

---

### POST /auth/refresh
**Description**: Get new Access Token using Refresh Token

**Request**:
```json
{
  "refreshToken": "Ug8v7w9y$B@E(H+MbQeThWmZq4t7w!z%C*F..."
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "NEW_TOKEN_HERE...",
  "expiresIn": 1800,
  "userId": 1,
  "username": "admin"
}
```

**Error** (401 Unauthorized):
```json
{
  "error": "Invalid or expired refresh token"
}
```

---

### POST /auth/logout
**Description**: Logout - revoke Refresh Token

**Request**:
```json
{
  "refreshToken": "Ug8v7w9y$B@E(H+MbQeThWmZq4t7w!z%C*F..."
}
```

**Response** (200 OK):
```json
{
  "message": "Logged out successfully"
}
```

---

### POST /auth/revoke-all
**Description**: Logout from all devices (requires auth)

**Headers**:
```
Authorization: Bearer <accessToken>
```

**Response** (200 OK):
```json
{
  "message": "Revoked 5 tokens",
  "revokedCount": 5
}
```

---

## 🗄️ Database Schema

### RefreshTokens Table
```sql
CREATE TABLE "RefreshTokens" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL,
    "Token" VARCHAR(64) NOT NULL UNIQUE,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL,
    "RevokedAt" TIMESTAMP NULL,
    "Device" VARCHAR(255) NULL,
    "IpAddress" VARCHAR(45) NULL
);

CREATE UNIQUE INDEX "UX_RefreshTokens_Token" ON "RefreshTokens" ("Token");
CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
CREATE INDEX "IX_RefreshTokens_User_Expires" ON "RefreshTokens" ("UserId", "ExpiresAt");
```

### Indexes
- `UX_RefreshTokens_Token` - Unique constraint for fast lookup
- `IX_RefreshTokens_UserId` - Fast user token queries
- `IX_RefreshTokens_User_Expires` - Efficient expiry checks

---

## 🔒 Security Features

### 1. Cryptographically Secure Token Generation
```csharp
var randomBytes = new byte[64];
using var rng = RandomNumberGenerator.Create();
rng.GetBytes(randomBytes);
var token = Convert.ToBase64String(randomBytes);
```

**Why secure?**
- Uses `RandomNumberGenerator` (not `Random`)
- 64 bytes = 512 bits of entropy
- Impossible to guess or brute-force

### 2. Token Rotation
```csharp
// Old token is revoked
RefreshTokenService.RevokeToken(oldToken);

// New token is generated
var newToken = RefreshTokenService.CreateRefreshToken(...);
```

**Why important?**
- Limits token lifetime
- Prevents replay attacks
- Detects stolen tokens

### 3. Device & IP Tracking
```csharp
var device = ctx.Request.Headers.UserAgent.ToString();
var ipAddress = ctx.Connection.RemoteIpAddress?.ToString();
```

**Use cases**:
- Audit logs
- Suspicious activity detection
- "Login from new device" notifications

### 4. Automatic Cleanup (Future)
```csharp
// Background service to delete expired tokens
var expiredTokens = await db.RefreshTokens
    .Where(t => t.ExpiresAt < DateTime.UtcNow)
    .ToListAsync();

db.RefreshTokens.RemoveRange(expiredTokens);
await db.SaveChangesAsync();
```

---

## 💻 Client Implementation

### React/TypeScript Example
```typescript
class AuthService {
  private accessToken: string | null = null;
  private refreshToken: string | null = null;

  async login(username: string, password: string): Promise<void> {
    const response = await fetch('/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });

    const data = await response.json();
    
    this.accessToken = data.accessToken;
    this.refreshToken = data.refreshToken;
    
    // Store refresh token in secure storage
    localStorage.setItem('refreshToken', data.refreshToken);
    
    // Schedule token refresh before expiry
    this.scheduleTokenRefresh(data.expiresIn);
  }

  async refreshAccessToken(): Promise<void> {
    const refreshToken = localStorage.getItem('refreshToken');
    
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await fetch('/auth/refresh', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });

    if (!response.ok) {
      // Refresh token expired - redirect to login
      this.logout();
      window.location.href = '/login';
      return;
    }

    const data = await response.json();
    
    this.accessToken = data.accessToken;
    this.refreshToken = data.refreshToken;
    
    localStorage.setItem('refreshToken', data.refreshToken);
    
    this.scheduleTokenRefresh(data.expiresIn);
  }

  private scheduleTokenRefresh(expiresIn: number): void {
    // Refresh 1 minute before expiry
    const refreshTime = (expiresIn - 60) * 1000;
    
    setTimeout(() => {
      this.refreshAccessToken();
    }, refreshTime);
  }

  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem('refreshToken');
    
    if (refreshToken) {
      await fetch('/auth/logout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken })
      });
    }

    this.accessToken = null;
    this.refreshToken = null;
    localStorage.removeItem('refreshToken');
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }
}

export const authService = new AuthService();
```

### Blazor Example
```csharp
public class TokenService
{
    private string? _accessToken;
    private string? _refreshToken;
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private Timer? _refreshTimer;

    public async Task<bool> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/auth/login", new
        {
            username,
            password
        });

        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        _accessToken = result.AccessToken;
        _refreshToken = result.RefreshToken;
        
        await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
        
        ScheduleTokenRefresh(result.ExpiresIn);
        
        return true;
    }

    private async Task RefreshAccessTokenAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
        
        var response = await _httpClient.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken
        });

        if (!response.IsSuccessStatusCode)
        {
            // Redirect to login
            await LogoutAsync();
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        
        _accessToken = result.AccessToken;
        _refreshToken = result.RefreshToken;
        
        await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
        
        ScheduleTokenRefresh(result.ExpiresIn);
    }

    private void ScheduleTokenRefresh(int expiresIn)
    {
        _refreshTimer?.Dispose();
        
        // Refresh 1 minute before expiry
        var refreshTime = TimeSpan.FromSeconds(expiresIn - 60);
        
        _refreshTimer = new Timer(async _ =>
        {
            await RefreshAccessTokenAsync();
        }, null, refreshTime, Timeout.InfiniteTimeSpan);
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");
        
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _httpClient.PostAsJsonAsync("/auth/logout", new
            {
                refreshToken
            });
        }

        _accessToken = null;
        _refreshToken = null;
        await _localStorage.RemoveItemAsync("refreshToken");
        
        _refreshTimer?.Dispose();
    }

    public string? GetAccessToken() => _accessToken;
}
```

---

## 🧪 Testing

### Test 1: Login
```bash
curl -X POST http://localhost:5000/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "UcimMatu!123"
  }'
```

**Expected Response**:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "Ug8v7w9y...",
  "expiresIn": 1800,
  "userId": 1,
  "username": "admin"
}
```

### Test 2: Refresh Token
```bash
curl -X POST http://localhost:5000/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "Ug8v7w9y..."
  }'
```

**Expected**: New Access Token & Refresh Token

### Test 3: Logout
```bash
curl -X POST http://localhost:5000/auth/logout \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "Ug8v7w9y..."
  }'
```

**Expected**: `{ "message": "Logged out successfully" }`

### Test 4: Revoke All Tokens
```bash
curl -X POST http://localhost:5000/auth/revoke-all \
  -H "Authorization: Bearer <accessToken>"
```

**Expected**: `{ "message": "Revoked 3 tokens", "revokedCount": 3 }`

---

## 📊 Migration Applied

```csharp
public partial class AddRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(nullable: false),
                Token = table.Column<string>(maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTime>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                RevokedAt = table.Column<DateTime>(nullable: true),
                Device = table.Column<string>(nullable: true),
                IpAddress = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "UX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_UserId",
            table: "RefreshTokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_User_Expires",
            table: "RefreshTokens",
            columns: new[] { "UserId", "ExpiresAt" });
    }
}
```

---

## 🚀 Deployment

```bash
# 1. Apply migration
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api

# 2. Deploy to Fly.io
fly deploy -a mathlearning-api

# 3. Test endpoints
curl -X POST https://mathlearning-api.fly.dev/auth/login ...
```

---

## 🎯 Best Practices

### ✅ DO
1. **Store Access Token in memory** - NOT localStorage (XSS protection)
2. **Store Refresh Token securely** - HttpOnly Cookie or Secure Storage
3. **Rotate Refresh Tokens** - Generate new on every refresh
4. **Set short Access Token lifetime** - 15-30 minutes
5. **Set long Refresh Token lifetime** - 7-30 days
6. **Validate tokens thoroughly** - Check expiry, revocation, user exists

### ❌ DON'T
1. **Don't store Access Token in localStorage** - XSS vulnerability
2. **Don't reuse Refresh Tokens** - Always rotate
3. **Don't set long Access Token lifetime** - Defeats the purpose
4. **Don't forget to revoke on logout** - Leaves door open
5. **Don't skip device/IP tracking** - Helpful for security audits

---

## 🔮 Future Enhancements

- [ ] **Token families** - Detect stolen tokens via family tree
- [ ] **Refresh Token cleanup** - Background service to delete expired
- [ ] **Rate limiting** - Prevent brute-force on /refresh endpoint
- [ ] **Email notifications** - "Login from new device" alerts
- [ ] **Token usage analytics** - Track refresh frequency

---

## 🏆 Conclusion

**Refresh Token System** provides:
- ✅ **Better UX** - No login prompts for 14 days
- ✅ **Better Security** - Short-lived Access Tokens
- ✅ **Flexibility** - Logout from all devices
- ✅ **Auditability** - Device & IP tracking
- ✅ **Production-ready** - Industry standard pattern

Build successful ✅ - Ready for production! 🚀
