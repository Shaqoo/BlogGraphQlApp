# Refresh Tokens & Login Lockout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single 24-hour JWT with DB-backed, rotated refresh tokens plus a 5-failed-attempt → 10-minute login lockout, and document both for the frontend.

**Architecture:** A new `RefreshToken` entity (SHA-256-hashed, revocable, rotated on each refresh with reuse detection) issues 30-min access tokens; `User` gains `FailedLoginAttempts`/`LockoutEndUtc` for the lockout. `AuthService` gains `RefreshTokenAsync`/`LogoutAsync`; `login`/`verifyEmail` return an `AuthResultDto` instead of a bare JWT string. A `RefreshTokenCleanupService` background job purges stale rows. A new root-level `AUTH_FRONTEND.md` documents the frontend changes.

**Tech Stack:** ASP.NET Core 8, HotChocolate 16 GraphQL, EF Core 8 + MySQL, Polly (already referenced), existing repository/UnitOfWork pattern, `ICacheService` (in-memory) for unknown-email throttling.

## Global Constraints

- **Do NOT commit or modify** the uncommitted user changes in the working tree: `Program.cs` (CORS `OnPrepareResponse` block near `UseStaticFiles`), `Services/Daily/DailyCallService.cs`, `Services/Groups/GroupCallService.cs`. Only ever stage files this plan creates/edits.
- Build/run tools (WSL host, Windows app): `dotnet = "/mnt/c/Program Files/dotnet/dotnet.exe"`; run the app from the project root so `DotNetEnv.Env.Load()` picks up `.env` (contains the real MySQL connection string via `ConnectionStrings__DefaultConnection`). Without `.env` the app cannot reach the DB.
- The app serves on `http://0.0.0.0:5000`; from WSL reach it at `http://192.168.192.1:5000` (never `127.0.0.1:5000`).
- MySQL client: `"/mnt/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h localhost -P 3306 -u root '-ppa$$word' blogapp_gql`.
- **No test project exists.** Verification is: Release build with 0 errors + live smoke tests (same approach as the prior hardening/WS work). No xUnit/Moq additions.
- Anonymous `/gql` is rate-limited to 10/min (login is anonymous). Pace smoke tests to stay under it or wait out the window between runs.
- Follow existing code style: `IUnitOfWork`/`IRepository<T>` (`T : BaseEntity`) for persistence, `ICacheService` for ephemeral state, `BackgroundService` pattern from `BackgroundServices/DailyRoomCleanupService.cs`, `ApiResponse<T>` result envelope, HotChocolate `[ExtendObjectType("Mutation")]`.
- JWT validation in `Program.cs` is unchanged (same issuer/audience/signing key); only the access-token *lifetime* shrinks.

---

### Task 1: Refresh token + lockout data model

**Files:**
- Create: `Entities/RefreshToken.cs`
- Create: `Configurations/EfConfigs/RefreshTokenConfiguration.cs`
- Modify: `Context/AppDbContext.cs` (add `DbSet<RefreshToken>` next to `DbSet<UserWebPushSubscription>` at line 27)
- Modify: `Repositories/Interfaces/IUnitOfWork.cs` (add property after `IRepository<User> Users`)
- Modify: `Repositories/Implementations/UnitOfWork.cs` (property + init in constructor)
- Modify: `Entities/User.cs` (add two lockout properties)
- Modify: `Configurations/EfConfigs/UserConfiguration.cs` (map the two new User properties)

**Interfaces:**
- Consumes: `BaseEntity` (`Entities/BaseEntity.cs`: `Guid Id`, `DateTime CreatedAt`, `DateTime UpdatedAt`, `bool IsDeleted`); `IRepository<T>`.
- Produces: `RefreshToken` entity (`Entities/RefreshToken.cs`) and `IRepository<RefreshToken> RefreshTokens` on `IUnitOfWork`; `User.FailedLoginAttempts` (int) and `User.LockoutEndUtc` (DateTime?).

- [ ] **Step 1: Create the `RefreshToken` entity**

`Entities/RefreshToken.cs`:

```csharp
using BlogGraphQlApp.Models;

namespace BlogGraphQlApp.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string? CreatedByIp { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
    }
}
```

- [ ] **Step 2: Create the EF configuration**

`Configurations/EfConfigs/RefreshTokenConfiguration.cs`:

```csharp
using BlogGraphQlApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BlogGraphQlApp.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(r => r.Id);
            builder.ToTable("RefreshTokens");

            builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(64);
            builder.HasIndex(r => r.TokenHash).IsUnique();

            builder.HasIndex(r => new { r.UserId, r.ExpiresAtUtc });

            builder.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

- [ ] **Step 3: Register the DbSet**

`Context/AppDbContext.cs`, after the `DbSet<UserWebPushSubscription>` line (line 27):

```csharp
public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
```

`ApplyConfigurationsFromAssembly` (line 85) auto-discovers `RefreshTokenConfiguration` — nothing else to wire in the context.

- [ ] **Step 4: Add the repository to UnitOfWork**

`Repositories/Interfaces/IUnitOfWork.cs`, after `IRepository<User> Users { get; }` (line 10):

```csharp
IRepository<RefreshToken> RefreshTokens { get; }
```

`Repositories/Implementations/UnitOfWork.cs` — add after `public IRepository<User> Users { get; }` (line 16):

```csharp
public IRepository<RefreshToken> RefreshTokens { get; }
```

and in the constructor after `Users = new Repository<User>(_context);` (line 61):

```csharp
RefreshTokens = new Repository<RefreshToken>(_context);
```

`RefreshToken` is in namespace `BlogGraphQlApp.Entities` — the `using BlogGraphQlApp.Entities;` already exists in both files.

- [ ] **Step 5: Add lockout fields to `User`**

`Entities/User.cs`, after `public string PasswordHash { get; set; } = string.Empty;` (line 16):

```csharp
public int FailedLoginAttempts { get; set; } = 0;
public DateTime? LockoutEndUtc { get; set; }
```

`Configurations/EfConfigs/UserConfiguration.cs`, before the relationship block (line 28):

```csharp
builder.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
```

- [ ] **Step 6: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q`
Expected: `0 Error(s)` (existing analyzer warnings are pre-existing and fine).

- [ ] **Step 7: Commit**

```bash
git add Entities/RefreshToken.cs Configurations/EfConfigs/RefreshTokenConfiguration.cs Context/AppDbContext.cs Repositories/Interfaces/IUnitOfWork.cs Repositories/Implementations/UnitOfWork.cs Entities/User.cs Configurations/EfConfigs/UserConfiguration.cs
git commit -m "feat: add refresh token entity and login lockout fields"
```

---

### Task 2: `AuthResultDto` + JWT/lockout config keys

**Files:**
- Create: `Dtos/AuthResultDto.cs`
- Modify: `appsettings.json` (Jwt section, line ~11-15)
- Modify: `.env` (append keys — real values; this file is gitignored)

**Interfaces:**
- Consumes: nothing new.
- Produces: `BlogGraphQlApp.DTOs.AuthResultDto { string AccessToken; string RefreshToken; int ExpiresIn; }`; config keys `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`, `Jwt:MaxLoginAttempts`, `Jwt:LoginLockoutMinutes`.

- [ ] **Step 1: Create the DTO**

`Dtos/AuthResultDto.cs`:

```csharp
namespace BlogGraphQlApp.DTOs
{
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
```

- [ ] **Step 2: Add placeholders to `appsettings.json`**

`appsettings.json`, inside the existing `"Jwt"` object (after line 14 `"Key": ...`):

```json
"AccessTokenMinutes": 30,
"RefreshTokenDays": 30,
"MaxLoginAttempts": 5,
"LoginLockoutMinutes": 10
```

- [ ] **Step 3: Append real values to `.env`**

Append these four lines to `.env` (non-secret values; do NOT commit `.env`):

```
Jwt__AccessTokenMinutes=30
Jwt__RefreshTokenDays=30
Jwt__MaxLoginAttempts=5
Jwt__LoginLockoutMinutes=10
```

- [ ] **Step 4: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q`
Expected: `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add Dtos/AuthResultDto.cs appsettings.json
git commit -m "feat: add auth result dto and JWT lockout config keys"
```

> `.env` is gitignored — it is intentionally NOT staged.

---

### Task 3: AuthService — issuance, refresh, logout, lockout

**Files:**
- Modify: `Services/Interfaces/IAuthService.cs`
- Modify: `Services/Implementations/AuthService.cs`

**Interfaces:**
- Consumes: `IUnitOfWork.RefreshTokens` (Task 1), `AuthResultDto` (Task 2), config keys (Task 2), `ICacheService`, `IHttpContextAccessor` (both already injected in `AuthService`).
- Produces (exact new signatures on `IAuthService`):
  - `Task<ApiResponse<AuthResultDto>> LoginAsync(string email, string password);`
  - `Task<ApiResponse<AuthResultDto>> VerifyEmailAsync(string email, string code);`
  - `Task<ApiResponse<AuthResultDto>> RefreshTokenAsync(string refreshToken);`
  - `Task<ApiResponse<bool>> LogoutAsync(string refreshToken);`

- [ ] **Step 1: Update the interface**

`Services/Interfaces/IAuthService.cs`, replace the three changed/changed-return lines:

```csharp
Task<ApiResponse<AuthResultDto>> LoginAsync(string email, string password);
Task<ApiResponse<AuthResultDto>> VerifyEmailAsync(string email, string code);
Task<ApiResponse<AuthResultDto>> RefreshTokenAsync(string refreshToken);
Task<ApiResponse<bool>> LogoutAsync(string refreshToken);
```

(`RequestVerificationCodeAsync`, `ForgotPasswordAsync`, `ResetPasswordAsync`, `GetCurrentUserAsync` stay as-is.)

- [ ] **Step 2: Add usings**

`Services/Implementations/AuthService.cs`, add to the existing usings:

```csharp
using BlogGraphQlApp.Entities;
using System.Security.Cryptography;
```

- [ ] **Step 3: Replace `LoginAsync`**

Replace the whole `LoginAsync` method (current lines 55-74) with:

```csharp
public async Task<ApiResponse<AuthResultDto>> LoginAsync(string email, string password)
{
    var normalizedEmail = email.Trim();
    var user = await _unitOfWork.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync();
    var now = DateTime.UtcNow;

    if (user is not null)
    {
        if (user.LockoutEndUtc is DateTime lockoutEnd && lockoutEnd > now)
        {
            return ApiResponse<AuthResultDto>.Fail(LockoutMessage);
        }

        if (user.LockoutEndUtc is not null)
        {
            user.LockoutEndUtc = null;
            user.FailedLoginAttempts = 0;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();
        }
    }
    else
    {
        var cachedFailures = await _cacheService.GetAsync<int>($"LoginFailures_{normalizedEmail}");
        if (cachedFailures >= MaxLoginAttempts)
        {
            return ApiResponse<AuthResultDto>.Fail("Invalid credentials.");
        }
    }

    if (user is null || !_encoder.Compare(password, user.PasswordHash))
    {
        if (user is not null)
        {
            user.FailedLoginAttempts += 1;
            if (user.FailedLoginAttempts >= MaxLoginAttempts)
            {
                user.LockoutEndUtc = now.AddMinutes(LoginLockoutMinutes);
                user.FailedLoginAttempts = 0;
            }
            _unitOfWork.Users.Update(user);
            await _unitOfWork.CompleteAsync();
        }
        else
        {
            var cachedFailures = await _cacheService.GetAsync<int>($"LoginFailures_{normalizedEmail}");
            await _cacheService.SetAsync($"LoginFailures_{normalizedEmail}", cachedFailures + 1, TimeSpan.FromMinutes(LoginLockoutMinutes));
        }

        return ApiResponse<AuthResultDto>.Fail("Invalid credentials.");
    }

    if (!user.IsEmailVerified)
    {
        var code = new Random().Next(100000, 999999).ToString();
        await _cacheService.SetAsync($"VerificationCode_{user.Email}", code, TimeSpan.FromMinutes(10));
        await _emailService.SendVerificationCodeAsync(user.Email, user.FullName, code);
        return ApiResponse<AuthResultDto>.Fail("Login successful, but your email is not verified. A new verification code has been sent to your email.");
    }

    if (user.FailedLoginAttempts != 0 || user.LockoutEndUtc is not null)
    {
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.CompleteAsync();
    }
    await _cacheService.RemoveAsync($"LoginFailures_{normalizedEmail}");

    var (accessToken, refreshToken, expiresIn) = await IssueTokenPairAsync(user);
    return ApiResponse<AuthResultDto>.Success(
        new AuthResultDto { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn },
        "Login successful.");
}
```

- [ ] **Step 4: Replace `VerifyEmailAsync` return type**

Change the signature line (current line 89) to `Task<ApiResponse<AuthResultDto>>` and the final two lines to:

```csharp
var (accessToken, refreshToken, expiresIn) = await IssueTokenPairAsync(user);
return ApiResponse<AuthResultDto>.Success(
    new AuthResultDto { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresIn = expiresIn },
    "Email verified successfully. You are now logged in.");
```

- [ ] **Step 5: Add refresh-token-revocation to `ResetPasswordAsync`**

After the existing `await _unitOfWork.CompleteAsync();` + `await _cacheService.RemoveAsync(cacheKey);` lines (current lines 150-152), insert:

```csharp
await RevokeAllUserTokensAsync(user.Id, DateTime.UtcNow);
```

- [ ] **Step 6: Add `RefreshTokenAsync` and `LogoutAsync`**

Insert after `GetCurrentUserAsync` (before the private helpers at line 156):

```csharp
public async Task<ApiResponse<AuthResultDto>> RefreshTokenAsync(string refreshToken)
{
    var hash = HashToken(refreshToken);
    var token = await _unitOfWork.RefreshTokens.Find(t => t.TokenHash == hash).FirstOrDefaultAsync();

    if (token is null)
    {
        return ApiResponse<AuthResultDto>.Fail("Invalid refresh token.");
    }

    var now = DateTime.UtcNow;

    if (token.RevokedAtUtc is not null)
    {
        await RevokeAllUserTokensAsync(token.UserId, now);
        _logger.LogWarning("Refresh token reuse detected; revoked all sessions for user {UserId}.", token.UserId);
        return ApiResponse<AuthResultDto>.Fail("Refresh token has been revoked.");
    }

    if (token.ExpiresAtUtc <= now)
    {
        _unitOfWork.RefreshTokens.Remove(token);
        await _unitOfWork.CompleteAsync();
        return ApiResponse<AuthResultDto>.Fail("Refresh token has expired.");
    }

    var user = await _unitOfWork.Users.GetByIdAsync(token.UserId);
    if (user is null)
    {
        _unitOfWork.RefreshTokens.Remove(token);
        await _unitOfWork.CompleteAsync();
        return ApiResponse<AuthResultDto>.Fail("User no longer exists.");
    }

    var (accessToken, rawRefresh, expiresIn) = await IssueTokenPairAsync(user, rotatedFrom: token);
    return ApiResponse<AuthResultDto>.Success(
        new AuthResultDto { AccessToken = accessToken, RefreshToken = rawRefresh, ExpiresIn = expiresIn },
        "Token refreshed successfully.");
}

public async Task<ApiResponse<bool>> LogoutAsync(string refreshToken)
{
    var hash = HashToken(refreshToken);
    var token = await _unitOfWork.RefreshTokens.Find(t => t.TokenHash == hash).FirstOrDefaultAsync();

    if (token is null || token.RevokedAtUtc is not null)
    {
        return ApiResponse<bool>.Success(true, "Logged out.");
    }

    token.RevokedAtUtc = DateTime.UtcNow;
    _unitOfWork.RefreshTokens.Update(token);
    await _unitOfWork.CompleteAsync();

    return ApiResponse<bool>.Success(true, "Logged out successfully.");
}
```

- [ ] **Step 7: Replace `GenerateJwtToken` and add the new private helpers**

Replace the entire `GenerateJwtToken` method (current lines 156-179) with:

```csharp
private (string Token, int ExpiresIn) GenerateAccessToken(User user)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var expires = DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = expires,
        Issuer = _configuration["Jwt:Issuer"],
        Audience = _configuration["Jwt:Audience"],
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return (tokenHandler.WriteToken(token), (int)Math.Round((expires - DateTime.UtcNow).TotalSeconds));
}

private async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> IssueTokenPairAsync(User user, RefreshToken? rotatedFrom = null)
{
    var (accessToken, expiresIn) = GenerateAccessToken(user);
    var rawRefresh = GenerateRefreshToken();

    var entity = new RefreshToken
    {
        UserId = user.Id,
        TokenHash = HashToken(rawRefresh),
        ExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays),
        CreatedByIp = GetClientIp()
    };

    if (rotatedFrom is not null)
    {
        rotatedFrom.RevokedAtUtc = DateTime.UtcNow;
        rotatedFrom.ReplacedByTokenId = entity.Id;
        _unitOfWork.RefreshTokens.Update(rotatedFrom);
    }

    await _unitOfWork.RefreshTokens.AddAsync(entity);
    await _unitOfWork.CompleteAsync();

    return (accessToken, rawRefresh, expiresIn);
}

private async Task RevokeAllUserTokensAsync(Guid userId, DateTime now)
{
    var activeTokens = await _unitOfWork.RefreshTokens
        .Find(t => t.UserId == userId && t.RevokedAtUtc == null)
        .ToListAsync();

    foreach (var token in activeTokens)
    {
        token.RevokedAtUtc = now;
        _unitOfWork.RefreshTokens.Update(token);
    }

    if (activeTokens.Count > 0)
    {
        await _unitOfWork.CompleteAsync();
    }
}

private static string GenerateRefreshToken()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

private static string HashToken(string token) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

private string? GetClientIp() =>
    _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

private static string LockoutMessage =>
    "Account is temporarily locked due to too many failed login attempts. Please try again later.";

private int AccessTokenMinutes => GetIntConfig("Jwt:AccessTokenMinutes", 30);
private int RefreshTokenDays => GetIntConfig("Jwt:RefreshTokenDays", 30);
private int MaxLoginAttempts => GetIntConfig("Jwt:MaxLoginAttempts", 5);
private int LoginLockoutMinutes => GetIntConfig("Jwt:LoginLockoutMinutes", 10);

private int GetIntConfig(string key, int fallback) =>
    int.TryParse(_configuration[key], out var value) ? value : fallback;
```

Note: `AuthService` already has `private readonly ILogger`? — **it does not.** Add a logger field. Update the constructor: add `ILogger<AuthService> logger` parameter and `_logger = logger;`, and add `private readonly ILogger<AuthService> _logger;` next to `_encoder`. (`ILogger<>` is in `Microsoft.Extensions.Logging` — already globally available via implicit usings.)

- [ ] **Step 8: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q`
Expected: `0 Error(s)`. Fix any name collisions (e.g. `System.Security.Claims.Claim` vs `ClaimTypes` — existing `using System.Security.Claims;` already resolves).

- [ ] **Step 9: Commit**

```bash
git add Services/Interfaces/IAuthService.cs Services/Implementations/AuthService.cs
git commit -m "feat: add refresh token rotation, logout, and login lockout to auth service"
```

---

### Task 4: GraphQL mutations

**Files:**
- Modify: `GraphQL/Mutations/AuthMutation.cs`

**Interfaces:**
- Consumes: `IAuthService` new signatures (Task 3), `AuthResultDto` (Task 2).
- Produces: GraphQL fields `login`, `verifyEmail` (return `ApiResponseOfAuthResult`), `refreshToken(input: RefreshTokenInput)`, `logout(input: LogoutInput)`.

- [ ] **Step 1: Add input records and change the two mutation return types**

`GraphQL/Mutations/AuthMutation.cs`:

Change `LoginAsync` (current line 39) to:

```csharp
public async Task<ApiResponse<AuthResultDto>> LoginAsync(
    LoginInput input,
    [Service] IAuthService authService)
{
    return await authService.LoginAsync(input.Email, input.Password);
}
```

Change `VerifyEmailAsync` (current line 70) to:

```csharp
public async Task<ApiResponse<AuthResultDto>> VerifyEmailAsync(
    VerifyEmailInput input,
    [Service] IAuthService authService)
{
    return await authService.VerifyEmailAsync(input.Email, input.Code);
}
```

Add two new input records next to the existing ones (after `ResetPasswordInput`, line ~35):

```csharp
public record RefreshTokenInput([Required] string RefreshToken);

public record LogoutInput([Required] string RefreshToken);
```

Add two new mutations before the closing brace of the class:

```csharp
[GraphQLDescription("Exchanges an unexpired refresh token for a new access/refresh token pair (rotation).")]
public async Task<ApiResponse<AuthResultDto>> RefreshTokenAsync(
    RefreshTokenInput input,
    [Service] IAuthService authService)
{
    return await authService.RefreshTokenAsync(input.RefreshToken);
}

[Authorize]
[GraphQLDescription("Revokes the presented refresh token (single-device logout).")]
public async Task<ApiResponse<bool>> LogoutAsync(
    LogoutInput input,
    [Service] IAuthService authService)
{
    return await authService.LogoutAsync(input.RefreshToken);
}
```

`AuthMutation.cs` already imports `BlogGraphQlApp.DTOs` (line 4) and `HotChocolate.Authorization`? — **it does not** import authorization. Add `using HotChocolate.Authorization;` (it uses `[Authorize]` — check: `UserMutation.cs` has it; `AuthMutation.cs` currently has no `[Authorize]`, so add the using only if `AuthorizeAttribute` unresolved, otherwise omit the attribute path is fine). Add the using unconditionally — it is the same package already referenced.

- [ ] **Step 2: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q`
Expected: `0 Error(s)`.

- [ ] **Step 3: Verify the schema**

Run the app (from project root):

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 > /tmp/opencode/auth.log 2>&1 &
```

Then introspect the mutation types:

```bash
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" -d '{"query":"{ __type(name: \"Mutation\") { fields { name } } }"}'
```

Expected: fields include `login`, `verifyEmail`, `refreshToken`, `logout`. Also confirm the login return type:

```bash
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" -d '{"query":"{ __type(name: \"Mutation\") { fields { name type { ofType { name } } } } }"}' | grep -iE "authresult|login"
```

Expected: `login`'s return type resolves to a type containing `AuthResult`.

- [ ] **Step 4: Stop the app**

```bash
kill $(cat /tmp/opencode/app.pid) 2>/dev/null; true
```

- [ ] **Step 5: Commit**

```bash
git add GraphQL/Mutations/AuthMutation.cs
git commit -m "feat: expose refreshToken and logout graphql mutations"
```

---

### Task 5: EF migration + live smoke tests

**Files:**
- Generate: `Migrations/<timestamp>_AddRefreshTokensAndLoginLockout.cs` (+ Designer, + snapshot update)
- Apply to local MySQL `blogapp_gql`

**Interfaces:**
- Consumes: schema from Tasks 1-4.
- Produces: `RefreshTokens` table (unique `TokenHash`, index on `(UserId, ExpiresAtUtc)`), `Users.FailedLoginAttempts` (int NOT NULL default 0), `Users.LockoutEndUtc` (datetime NULL).

- [ ] **Step 1: Add the migration**

Run (from project root; `.env` supplies the connection string):

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" ef migrations add AddRefreshTokensAndLoginLockout
```

Expected: migration files created under `Migrations/`. If `dotnet ef` is missing, install the tool first:
`"/mnt/c/Program Files/dotnet/dotnet.exe" tool install --global dotnet-ef` then re-run.

- [ ] **Step 2: Apply the migration**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" ef database update
```

- [ ] **Step 3: Verify schema in MySQL**

```bash
"/mnt/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h localhost -P 3306 -u root '-ppa$$word' blogapp_gql -e "SHOW TABLES LIKE 'RefreshTokens'; SHOW COLUMNS FROM RefreshTokens; SHOW COLUMNS FROM Users LIKE '%Login%'; SHOW COLUMNS FROM Users LIKE '%Lockout%';"
```

Expected: `RefreshTokens` exists with `TokenHash`, `ExpiresAtUtc`, `RevokedAtUtc`, `ReplacedByTokenId`, `CreatedByIp`, `UserId`; `Users` has `FailedLoginAttempts` (int, default 0) and `LockoutEndUtc` (nullable datetime).

- [ ] **Step 4: Start the app**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 > /tmp/opencode/auth.log 2>&1 &
```

- [ ] **Step 5: Login returns an `AuthResult`**

```bash
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" \
  -d '{"query":"mutation { login(input: { email: \"rluser1@yopmail.com\", password: \"Test@1234\" }) { succeeded message data { accessToken refreshToken expiresIn } errors } }"}'
```

Expected: `succeeded: true`, `data.accessToken` (a JWT starting `eyJ...`), `data.refreshToken` (a ~43-char base64url string), `data.expiresIn` ≈ 1800. Save the refresh token to `/tmp/opencode/rt1`.

- [ ] **Step 6: Refresh rotates the token**

```bash
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" \
  -d '{"query":"mutation($rt: String!) { refreshToken(input: { refreshToken: $rt }) { succeeded message data { accessToken refreshToken expiresIn } errors } }","variables":{"rt":"<rt1>"}}'
```

Expected: `succeeded: true`, new `refreshToken` ≠ old. Then re-present the OLD `rt1`:
Expected: `succeeded: false`, message `Refresh token has been revoked.` — and (reuse detection) all the user's other sessions are revoked too (verify by attempting to refresh the NEW token: `succeeded: false`).

- [ ] **Step 7: Logout revokes the session**

Login again, take `rt2`, call `logout(input: { refreshToken: "<rt2>" })` with an `Authorization: Bearer <access>` header. Then `refreshToken` with `rt2` → `succeeded: false`.

- [ ] **Step 8: Lockout — 5 failed attempts then a 10-min ban**

Run 5 logins with wrong password for `rluser1@yopmail.com`, then a 6th with the correct password `Test@1234`:

```bash
for i in 1 2 3 4 5; do curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" \
  -d '{"query":"mutation { login(input: { email: \"rluser1@yopmail.com\", password: \"WrongPass1!\" }) { succeeded message } }"}' ; echo; done
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" \
  -d '{"query":"mutation { login(input: { email: \"rluser1@yopmail.com\", password: \"Test@1234\" }) { succeeded message } }"}' ; echo
```

Expected: attempts 1-5 → `succeeded: false` / `Invalid credentials.`; attempt 6 (correct password) → `succeeded: false` / `Account is temporarily locked due to too many failed login attempts. Please try again later.`

Pacing note: the anonymous `/gql` rate limit is 10/min. This step uses ~7 anonymous requests; keep the refresh/logout steps under the same minute or wait before this block.

Verify the DB state:

```bash
"/mnt/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h localhost -P 3306 -u root '-ppa$$word' blogapp_gql -e "SELECT Email, FailedLoginAttempts, LockoutEndUtc FROM Users WHERE Email='rluser1@yopmail.com';"
```

Expected: `LockoutEndUtc` ≈ now+10 min, `FailedLoginAttempts = 0` (reset when the ban was set).

- [ ] **Step 9: Confirm lockout clears after the window (no 10-min wait)**

Manually clear the ban in MySQL, then confirm a correct login succeeds and the counter stays clean:

```bash
"/mnt/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h localhost -P 3306 -u root '-ppa$$word' blogapp_gql -e "UPDATE Users SET LockoutEndUtc = NULL WHERE Email='rluser1@yopmail.com';"
curl -s -X POST http://192.168.192.1:5000/gql -H "Content-Type: application/json" \
  -d '{"query":"mutation { login(input: { email: \"rluser1@yopmail.com\", password: \"Test@1234\" }) { succeeded message data { accessToken } } }"}'
```

Expected: `succeeded: true`.

- [ ] **Step 10: Password reset revokes sessions (code-review verification)**

The `resetPassword` flow's reset token lives only in the in-memory cache (`PasswordResetToken_{email}`) and is emailed out, so it can't be obtained externally for a live test. Verify by reading the Task 3 Step 5 change: `ResetPasswordAsync` calls `RevokeAllUserTokensAsync(user.Id, ...)` after saving the new password. Confirm the method is present and compiles (Task 3 build already proved this). No live execution.

- [ ] **Step 11: Clean up + stop app**

```bash
kill $(cat /tmp/opencode/app.pid) 2>/dev/null; true
```

Restore rluser1's state if you changed anything beyond `LockoutEndUtc` (which Step 9 already cleared).

- [ ] **Step 12: Commit**

```bash
git add Migrations/
git commit -m "feat: add refresh token and login lockout migration"
```

---

### Task 6: Refresh token cleanup service

**Files:**
- Create: `BackgroundServices/RefreshTokenCleanupService.cs`
- Modify: `Program.cs` (add one `AddHostedService` line near line 165)

**Interfaces:**
- Consumes: `IUnitOfWork.RefreshTokens` (Task 1).
- Produces: nothing consumed elsewhere; a hosted service registered in DI.

- [ ] **Step 1: Create the service**

`BackgroundServices/RefreshTokenCleanupService.cs`:

```csharp
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogGraphQlApp.BackgroundServices
{
    public class RefreshTokenCleanupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan RevokedRetention = TimeSpan.FromDays(30);

        private readonly IServiceProvider _services;
        private readonly ILogger<RefreshTokenCleanupService> _logger;

        public RefreshTokenCleanupService(IServiceProvider services, ILogger<RefreshTokenCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Refresh token cleanup service started.");

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanUpAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Refresh token cleanup cycle failed.");
                }
            }
        }

        private async Task CleanUpAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = DateTime.UtcNow;
            var stale = await uow.RefreshTokens
                .Find(t => t.ExpiresAtUtc <= now ||
                           (t.RevokedAtUtc != null && t.RevokedAtUtc <= now - RevokedRetention))
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return;
            }

            uow.RefreshTokens.RemoveRange(stale);
            await uow.CompleteAsync(cancellationToken);
            _logger.LogInformation("Refresh token cleanup removed {Count} stale tokens.", stale.Count);
        }
    }
}
```

- [ ] **Step 2: Register it**

`Program.cs`, after `builder.Services.AddHostedService<DailyRoomCleanupService>();` (line 165):

```csharp
builder.Services.AddHostedService<RefreshTokenCleanupService>();
```

Do NOT touch anything else in `Program.cs` (the working tree has unrelated uncommitted CORS changes there).

- [ ] **Step 3: Build + confirm it starts**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build BlogGraphQlApp.csproj -c Release --nologo -v q` → `0 Error(s)`.

Then run the app briefly and grep the log for the startup line:

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Release/net8.0/BlogGraphQlApp.dll --urls http://0.0.0.0:5000 > /tmp/opencode/auth.log 2>&1 &
sleep 10
grep -i "refresh token cleanup" /tmp/opencode/auth.log
kill $(cat /tmp/opencode/app.pid) 2>/dev/null; true
```

Expected: `Refresh token cleanup service started.`

- [ ] **Step 4: Commit**

```bash
git add BackgroundServices/RefreshTokenCleanupService.cs Program.cs
git commit -m "feat: add refresh token cleanup background service"
```

---

### Task 7: `AUTH_FRONTEND.md`

**Files:**
- Create: `AUTH_FRONTEND.md` (repo root)

**Interfaces:**
- Consumes: the final GraphQL schema from Tasks 3-5.
- Produces: the frontend-facing doc.

- [ ] **Step 1: Write the document**

`AUTH_FRONTEND.md`:

```markdown
# Authentication — Refresh Tokens & Login Lockout (Frontend Guide)

All GraphQL operations are at `POST /gql`. Auth is now a **two-token** scheme: a short-lived
access token plus a long-lived, one-time-use refresh token.

## What changed

- `login` and `verifyEmail` no longer return a raw JWT string in `data`. They now return an
  `AuthResult` object.
- Access tokens last **30 minutes** (down from 24 hours) and must be refreshed.
- Refresh tokens last **30 days** and are **rotated**: every refresh consumes the old one and
  issues a fresh pair. A refresh token is single-use.
- After **5 failed login attempts**, an account is locked for **10 minutes**.
- A `logout` mutation revokes a device's refresh token server-side.

## Login / verifyEmail — new response shape

Mutation:

```graphql
mutation Login($email: String!, $password: String!) {
  login(input: { email: $email, password: $password }) {
    succeeded
    message
    data {
      accessToken
      refreshToken
      expiresIn
    }
    errors
  }
}
```

`expiresIn` is the access-token lifetime in seconds (1800). Store both tokens; `refreshToken`
must be sent ONLY to the `refreshToken` and `logout` mutations — never as an `Authorization`
header.

## Refreshing an expired access token

Mutation (no auth header needed):

```graphql
mutation Refresh($refreshToken: String!) {
  refreshToken(input: { refreshToken: $refreshToken }) {
    succeeded
    message
    data {
      accessToken
      refreshToken
      expiresIn
    }
    errors
  }
}
```

Client flow:

1. Attach `Authorization: Bearer <accessToken>` to every authenticated request and WS
   `connection_init` (GraphQL-over-WebSocket uses the `Authorization` header; the SignalR
   presence hub uses the `?access_token=` query param).
2. On an `Unauthorized`/token error, call `refreshToken` once with the stored refresh token.
3. On success, replace both stored tokens and retry the original request. Keep a single
   in-flight refresh promise so concurrent 401s share it.
4. On failure, the refresh token is invalid/expired/revoked → clear local tokens and route to
   the login screen.

## Logout (revokes the device session)

```graphql
mutation Logout($refreshToken: String!) {
  logout(input: { refreshToken: $refreshToken }) {
    succeeded
    message
  }
}
```

Requires the `Authorization` header. Afterwards clear both tokens locally.

## Error messages you may show

| Operation | `message` |
| --- | --- |
| Bad email/password | `Invalid credentials.` |
| Account locked | `Account is temporarily locked due to too many failed login attempts. Please try again later.` |
| Refresh token invalid | `Invalid refresh token.` |
| Refresh token reused/revoked | `Refresh token has been revoked.` |
| Refresh token expired | `Refresh token has expired.` |
| Unverified email on login | `Login successful, but your email is not verified. A new verification code has been sent to your email.` |

## Lockout UX

After 5 failed logins the account is locked for 10 minutes (even with the correct password).
Show the lockout message and disable the submit button (a countdown is nice-to-have). Unknown
emails always get `Invalid credentials.` — never reveal whether an account exists.

## Security notes

- Never log tokens or put the refresh token in a URL.
- The refresh token is single-use; if a client replays an old one, the backend revokes all of
  that user's sessions as a compromise response.
```

- [ ] **Step 2: Commit**

```bash
git add AUTH_FRONTEND.md
git commit -m "docs: add auth refresh token and lockout frontend guide"
```

---

## Self-Review Notes

- **Spec coverage:** data model (Task 1), issuance/lifetimes (Tasks 2-3), refresh+rotation+reuse
  (Task 3 Step 6), logout (Task 3 Step 6), lockout incl. unknown emails (Task 3 Step 3),
  password-reset revocation (Task 3 Step 5), cleanup service (Task 6), `AUTH_FRONTEND.md`
  (Task 7), config keys (Task 2), migration+smoke tests (Task 5).
- **Deviations from spec (intentional):** reuse detection revokes **all** of the user's tokens
  rather than only the rotated chain — strictly stronger and simpler; the `RefreshToken` entity
  uses `BaseEntity.CreatedAt` for its creation timestamp rather than a separate `CreatedAtUtc`
  column.
- **Type consistency:** `AuthResultDto` property names (`AccessToken`/`RefreshToken`/`ExpiresIn`)
  are identical across Tasks 2-4 and the doc; `IAuthService` signatures used in Task 4 match
  Task 3; `IRepository<RefreshToken> RefreshTokens` used in Tasks 3 and 6 matches Task 1.
