# 📱 Mobile User Registration System - Complete Guide

## 🎯 Overview

Implementiran **kompletan sistem za registraciju mobilnih korisnika** koji omogućava:
- 📱 **Public registration** - Bilo ko može da kreira nalog
- 🔐 **Automatic authentication** - Auto-login nakon registracije
- 💰 **Welcome bonus** - 100 coins za nove korisnike
- 👤 **User profiles** - Username, DisplayName, Stats
- 🔒 **Secure** - Password hashing, JWT tokens, Refresh tokens

---

## 🛠️ API Endpoints

### POST /auth/mobile/register
**Description**: Register new mobile user (public endpoint)

**Request**:
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecurePass123",
  "displayName": "John Doe"
}
```

**Validation Rules**:
- `username`: Min 3 characters, unique
- `email`: Valid email format, unique
- `password`: Min 6 characters
- `displayName`: Optional, defaults to username

**Response** (200 OK - Success):
```json
{
  "success": true,
  "message": "Registration successful",
  "tokens": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "Ug8v7w9y$B@E(H+MbQeThWmZq4t7w!z%C*F...",
    "expiresIn": 1800,
    "userId": 123,
    "username": "john_doe"
  },
  "profile": {
    "userId": 123,
    "username": "john_doe",
    "displayName": "John Doe",
    "coins": 100,
    "level": 1,
    "xp": 0,
    "streak": 0,
    "createdAt": "2026-01-23T15:30:00Z"
  }
}
```

**Response** (400 Bad Request - Validation Error):
```json
{
  "success": false,
  "message": "Username must be at least 3 characters long"
}
```

**Response** (409 Conflict - Username Taken):
```json
{
  "success": false,
  "message": "Username already taken"
}
```

**Response** (409 Conflict - Email Exists):
```json
{
  "success": false,
  "message": "Email already registered"
}
```

---

### GET /api/users/profile
**Description**: Get current user's profile

**Headers**:
```
Authorization: Bearer <accessToken>
```

**Response**:
```json
{
  "userId": 123,
  "username": "john_doe",
  "displayName": "John Doe",
  "coins": 95,
  "level": 2,
  "xp": 150,
  "streak": 5,
  "createdAt": "2026-01-23T15:30:00Z"
}
```

---

### PUT /api/users/profile
**Description**: Update user profile

**Headers**:
```
Authorization: Bearer <accessToken>
```

**Request**:
```json
{
  "displayName": "John \"MathPro\" Doe"
}
```

**Response**:
```json
{
  "userId": 123,
  "username": "john_doe",
  "displayName": "John \"MathPro\" Doe",
  "coins": 95,
  "level": 2,
  "xp": 150,
  "streak": 5,
  "createdAt": "2026-01-23T15:30:00Z"
}
```

---

### GET /api/users/stats
**Description**: Get detailed user statistics

**Response**:
```json
{
  "profile": {
    "userId": 123,
    "username": "john_doe",
    "displayName": "John Doe",
    "coins": 95,
    "level": 2,
    "xp": 150,
    "streak": 5,
    "createdAt": "2026-01-23T15:30:00Z"
  },
  "stats": {
    "totalQuestions": 50,
    "totalAttempts": 75,
    "totalCorrect": 60,
    "accuracy": 80.0,
    "hintsUsed": 5,
    "coins": {
      "current": 95,
      "earned": 150,
      "spent": 55
    }
  }
}
```

---

### GET /api/users/search?query={query}&limit={limit}
**Description**: Search users by username or display name

**Query Parameters**:
- `query` - Search term (min 2 characters)
- `limit` - Max results (default: 10)

**Response**:
```json
[
  {
    "userId": 123,
    "username": "john_doe",
    "displayName": "John Doe",
    "level": 2,
    "xp": 150
  },
  {
    "userId": 456,
    "username": "jane_smith",
    "displayName": "Jane Smith",
    "level": 5,
    "xp": 500
  }
]
```

---

## 🔄 Complete Mobile App Flow

### 1. Registration Flow
```
User opens app (first time)
↓
User fills registration form:
  - Username: "john_doe"
  - Email: "john@example.com"
  - Password: "SecurePass123"
  - Display Name: "John Doe"
↓
App → POST /auth/mobile/register
↓
Server:
  1. Validates input
  2. Checks username/email uniqueness
  3. Creates IdentityUser (ASP.NET Identity)
  4. Creates UserProfile (with 100 coins welcome bonus)
  5. Generates Access Token (30 min)
  6. Generates Refresh Token (14 days)
  7. Stores Refresh Token in database
↓
Server → { success: true, tokens: {...}, profile: {...} }
↓
App:
  1. Stores Access Token in memory
  2. Stores Refresh Token in SecureStorage
  3. Navigates to Home Screen
  4. Shows "Welcome! You have 100 coins to start!"
```

### 2. Auto-Login on Subsequent Opens
```
User opens app (has Refresh Token stored)
↓
App checks SecureStorage
↓
Refresh Token found!
↓
App → POST /auth/refresh { refreshToken: "..." }
↓
Server → { accessToken: "...", refreshToken: "..." }
↓
App updates tokens
↓
App navigates to Home Screen (auto-logged in)
```

### 3. Profile Update Flow
```
User navigates to Settings
↓
User changes Display Name to "MathMaster"
↓
App → PUT /api/users/profile { displayName: "MathMaster" }
↓
Server updates UserProfile.DisplayName
↓
Server → Updated profile
↓
App updates UI
```

---

## 💻 Mobile Client Implementation (Flutter/Dart)

### Registration
```dart
class AuthService {
  Future<RegisterResponse> register({
    required String username,
    required String email,
    required String password,
    String? displayName,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/mobile/register'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'username': username,
        'email': email,
        'password': password,
        'displayName': displayName,
      }),
    );

    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      
      // Store tokens
      await _storage.write(key: 'accessToken', value: data['tokens']['accessToken']);
      await _storage.write(key: 'refreshToken', value: data['tokens']['refreshToken']);
      
      return RegisterResponse.fromJson(data);
    } else {
      final error = jsonDecode(response.body);
      throw Exception(error['message']);
    }
  }
}
```

### Auto-Login
```dart
class AuthService {
  Future<bool> tryAutoLogin() async {
    final refreshToken = await _storage.read(key: 'refreshToken');
    
    if (refreshToken == null) return false;

    try {
      final response = await http.post(
        Uri.parse('$baseUrl/auth/refresh'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'refreshToken': refreshToken}),
      );

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        
        await _storage.write(key: 'accessToken', value: data['accessToken']);
        await _storage.write(key: 'refreshToken', value: data['refreshToken']);
        
        return true;
      }
    } catch (e) {
      print('Auto-login failed: $e');
    }

    return false;
  }
}
```

### Get Profile
```dart
class UserService {
  Future<UserProfile> getProfile() async {
    final accessToken = await _storage.read(key: 'accessToken');
    
    final response = await http.get(
      Uri.parse('$baseUrl/api/users/profile'),
      headers: {
        'Authorization': 'Bearer $accessToken',
      },
    );

    if (response.statusCode == 200) {
      return UserProfile.fromJson(jsonDecode(response.body));
    } else {
      throw Exception('Failed to load profile');
    }
  }
}
```

---

## 🔐 Security Features

### 1. Password Hashing
```csharp
// ASP.NET Identity automatically hashes passwords
var result = await userManager.CreateAsync(user, request.Password);
// Password is NEVER stored in plain text
```

### 2. JWT Token
```
Payload:
{
  "sub": "user-id",
  "unique_name": "john_doe",
  "userId": "123",
  "jti": "unique-token-id",
  "exp": 1706026800
}

Signed with: HS256 (HMAC-SHA256)
Secret: 32+ character secret key
```

### 3. Refresh Token
```
- 64 bytes cryptographically secure random
- Stored in database (server-side validation)
- 14 days lifetime
- Rotated on every refresh (old token revoked)
```

### 4. Input Validation
```csharp
// Server-side validation
if (username.Length < 3) return BadRequest("Username too short");
if (!email.Contains('@')) return BadRequest("Invalid email");
if (password.Length < 6) return BadRequest("Password too short");

// Username uniqueness check
var existing = await userManager.FindByNameAsync(username);
if (existing != null) return Conflict("Username taken");
```

---

## 📊 Database Schema Updates

### UserProfiles Table (Updated)
```sql
ALTER TABLE "UserProfiles"
ADD COLUMN "DisplayName" VARCHAR(256) NULL;

CREATE INDEX "IX_UserProfiles_DisplayName" 
ON "UserProfiles" ("DisplayName");
```

**Why DisplayName?**
- Username is unique identifier (can't change)
- DisplayName is friendly name (can change)
- Example: Username = "john_doe123", DisplayName = "John Doe"

---

## 🧪 Testing

### Test 1: Register New User
```bash
curl -X POST http://localhost:5000/auth/mobile/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "test_user",
    "email": "test@example.com",
    "password": "TestPass123",
    "displayName": "Test User"
  }'
```

**Expected**: 200 OK with tokens and profile

### Test 2: Register Duplicate Username
```bash
curl -X POST http://localhost:5000/auth/mobile/register \
  -d '{"username": "test_user", ...}'
```

**Expected**: 409 Conflict with "Username already taken"

### Test 3: Get Profile
```bash
curl http://localhost:5000/api/users/profile \
  -H "Authorization: Bearer <accessToken>"
```

**Expected**: User profile data

### Test 4: Update Display Name
```bash
curl -X PUT http://localhost:5000/api/users/profile \
  -H "Authorization: Bearer <accessToken>" \
  -d '{"displayName": "New Name"}'
```

**Expected**: Updated profile

### Test 5: Search Users
```bash
curl "http://localhost:5000/api/users/search?query=test"
```

**Expected**: List of users matching "test"

---

## 🚀 Deployment

```bash
# 1. Apply migration
cd src/MathLearning.Infrastructure
dotnet ef database update --context ApiDbContext --startup-project ../MathLearning.Api

# 2. Deploy to Fly.io
cd ../..
fly deploy

# 3. Test registration
curl -X POST https://mathlearning-api.fly.dev/auth/mobile/register \
  -d '{"username":"mobile_user","email":"mobile@example.com","password":"Pass123"}'
```

---

## 📋 Validation Rules Summary

| Field | Min Length | Max Length | Format | Unique | Required |
|-------|-----------|-----------|--------|--------|----------|
| Username | 3 | 256 | Alphanumeric + underscore | Yes | Yes |
| Email | N/A | 256 | Valid email format | Yes | Yes |
| Password | 6 | N/A | Any characters | No | Yes |
| DisplayName | 0 | 256 | Any characters | No | No |

---

## 🎯 Best Practices

### ✅ DO
1. **Store Refresh Token securely** - Use SecureStorage/Keychain
2. **Validate on server** - Never trust client validation
3. **Use HTTPS** - Always encrypt traffic
4. **Handle errors gracefully** - Show user-friendly messages
5. **Auto-login** - Check Refresh Token on app start

### ❌ DON'T
1. **Don't store passwords** - Only tokens
2. **Don't expose sensitive data** - Password hashes, etc.
3. **Don't skip validation** - Always validate input
4. **Don't hardcode secrets** - Use environment variables
5. **Don't forget to logout** - Revoke tokens on logout

---

## 🏆 Conclusion

**Mobile User Registration System** provides:
- ✅ **Public registration** - Anyone can create account
- ✅ **Secure authentication** - JWT + Refresh tokens
- ✅ **Welcome bonus** - 100 coins for new users
- ✅ **User profiles** - Username, DisplayName, Stats
- ✅ **Auto-login** - Seamless user experience
- ✅ **Production-ready** - Validated, secured, tested

Build successful ✅ - Ready for mobile app integration! 📱🚀
