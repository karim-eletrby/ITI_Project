# Connectly — Architectural & Technical Reference

> **Solution:** `WebApplication1.slnx`  
> **Product:** Connectly — a social networking application (posts, friends, chat, notifications).  
> **Last reviewed:** September 2026, against the live source tree.

This document describes the **actual** implementation. It does not assume e-commerce concepts (`Product`, `Cart`, `Order`) because they do not exist in this codebase.

---

## Table of Contents

1. [Executive Summary & Technology Stack](#1-executive-summary--technology-stack)
2. [Solution Architecture & Layer Breakdown](#2-solution-architecture--layer-breakdown)
3. [Core Architectural Patterns & Design Approaches](#3-core-architectural-patterns--design-approaches)
4. [End-to-End Request Flows & Data Lifecycles](#4-end-to-end-request-flows--data-lifecycles)
5. [SignalR Implementation & Real-Time Communications](#5-signalr-implementation--real-time-communications)
6. [API Catalog & Endpoint Directory](#6-api-catalog--endpoint-directory)

---

## 1. Executive Summary & Technology Stack

### Executive Summary

Connectly is a **.NET 10** full-stack social application combining:

- **ASP.NET Core MVC** — server-rendered Razor pages for the browser UI (`Feed`, `Profile`, `Chat`, `Auth`, etc.).
- **ASP.NET Core Web API** — JSON REST endpoints under `/api/*` consumed by the SPA-style frontend JavaScript.
- **ASP.NET Core Identity** — user accounts, password hashing, and role storage.
- **JWT Bearer + MVC Cookie** — dual authentication: Bearer tokens for API/SignalR; an HttpOnly `MvcCookie` for same-origin page navigation after token exchange.
- **Entity Framework Core 10 + SQL Server** — relational persistence with code-first migrations.
- **SignalR** — real-time chat messages, typing indicators, and in-app notifications.

The solution follows a **layered architecture** inspired by Clean/Onion principles (`Domain` → `Infrastructure` → `Application` → `Presentation`), with pragmatic deviations documented in [§2](#2-solution-architecture--layer-breakdown).

### Framework & Runtime

| Aspect | Value |
|--------|-------|
| **Target framework** | `net10.0` (all projects) |
| **Hosting model** | ASP.NET Core (`Microsoft.NET.Sdk.Web` in `Presentation`) |
| **Web server** | Kestrel (default); max request body **1 GB** for large video uploads |
| **API runtime** | Controllers with `[ApiController]`; JSON camelCase via `System.Text.Json` |
| **Entry point** | `Presentation/Program.cs` |

### Core Libraries & Packages

Packages are declared per project. There is **no** AutoMapper, Mapster, or FluentValidation in this solution — mapping and validation are hand-written in services and controllers.

#### `Presentation` (`Presentation.csproj`)

| Package | Responsibility |
|---------|----------------|
| `Microsoft.EntityFrameworkCore.Design` 10.0.11 | Design-time EF tooling (`dotnet ef migrations`) |

Project references: `Application`, `Infrastructure`.

#### `Infrastructure` (`Infrastructure.csproj`)

| Package | Responsibility |
|---------|----------------|
| `Microsoft.EntityFrameworkCore.SqlServer` 10.0.11 | SQL Server provider for EF Core |
| `Microsoft.EntityFrameworkCore.Tools` 10.0.11 | CLI migration commands |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11 | Identity stores backed by EF Core |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 | JWT validation for API and SignalR |
| `System.IdentityModel.Tokens.Jwt` 8.22.0 | JWT creation (used indirectly via Application `TokenService`) |
| `Microsoft.AspNetCore.App` (framework reference) | ASP.NET Core hosting primitives |

#### `Application` (`Application.csproj`)

| Reference | Responsibility |
|-----------|----------------|
| `Microsoft.AspNetCore.App` | SignalR hubs, `IWebHostEnvironment`, Identity APIs in services |
| Project ref → `Domain`, `Infrastructure` | Entities/DTOs and `ApplicationDbContext` access |

#### `Domain` (`Domain.csproj`)

| Package | Responsibility |
|---------|----------------|
| `Microsoft.Extensions.Identity.Stores` 10.0.11 | `IdentityUser` base type for `ApplicationUser` |

### Data Stores & External Services

| Store / Service | Configuration | Purpose |
|-----------------|---------------|---------|
| **SQL Server** | `ConnectionStrings:Conn` in `appsettings.json` | Primary database (`ConnectlyDb`) |
| **EF Core Migrations** | `Infrastructure/Migrations/`; assembly = `Infrastructure` | Schema versioning; applied on startup via `Program.cs` |
| **Local filesystem** | `wwwroot/uploads/{profiles,covers,posts}/` | Profile/cover images and post media via `LocalFileStorageService` |
| **SMTP (Gmail-compatible)** | `Smtp:*` section; secrets via **User Secrets** (`connectly-presentation-dev`) or environment variables | OTP emails, email-change security alerts |
| **User Secrets** | `UserSecretsId` on `Presentation.csproj` | Development storage for JWT key, SMTP password, OTP pepper |

**Production guard:** `ProductionConfigurationValidator` runs at startup when `IsProduction()` and rejects missing/weak JWT key, OTP pepper, SMTP, connection string, or localhost `App:PublicUrl`.

**JWT timing** (from `appsettings.json`):

```json
"Jwt": {
  "DurationInMinutes": 60,
  "RefreshTokenDurationInDays": 7
}
```

**OTP timing** (from `appsettings.json` / `OtpOptions`):

```json
"Otp": {
  "ExpiryMinutes": 10,
  "MaxAttempts": 5,
  "ResendCooldownSeconds": 60
}
```

Note: `EmailOtpService` hard-codes OTP expiry to **10 minutes** in entity creation; `OtpOptions.ExpiryMinutes` is bound but not currently read when setting `ExpiresAt`.

---

## 2. Solution Architecture & Layer Breakdown

### Solution Structure

```
WebApplication1/                    ← solution root
├── Domain/                         ← entities, enums, response envelopes
├── Infrastructure/                 ← EF Core, Identity/JWT DI, Fluent API configs, migrations
├── Application/                    ← services, repositories, UoW, DTOs, SignalR hubs
├── Presentation/                   ← Program.cs, API + MVC controllers, middleware, wwwroot
└── WebApplication1.slnx
```

### Dependency Graph (actual)

```
Presentation
    ├── Application
    │       ├── Domain
    │       └── Infrastructure  ← Application directly references Infrastructure (DbContext)
    └── Infrastructure
            └── Domain
```

**Deviation from strict Clean Architecture:** `Application` references `Infrastructure` because repositories take `ApplicationDbContext` directly. In a purist onion layout, `Application` would depend only on abstractions and Infrastructure would implement them. Here, repositories live in `Application/Repositories/` but depend on `Infrastructure.Context.ApplicationDbContext`.

---

### 2.1 Domain Layer (`Domain/`)

The Domain layer contains **no business services** — only entities, enums, and shared response types.

#### Base Types

**`BaseEntity<TKey>`** — common audit and soft-delete fields for most persisted entities:

```7:12:Domain/Common/BaseEntity.cs
    public abstract class BaseEntity<TKey>
    {
        public TKey Id { get; set; } = default!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
```

**Response envelopes** (used by API layer):

- `SuccessResponse<T>` — `{ success, message, data }`
- `ErrorResponse` — `{ success, message, errors, fieldErrors, data }`

#### Entities

| Entity | Base | Key | Description |
|--------|------|-----|-------------|
| **`ApplicationUser`** | `IdentityUser` | `string` (GUID) | Extended Identity user: display name, bio, profile/cover URLs, date of birth, social navigation collections |
| **`Post`** | `BaseEntity<int>` | `int` | User-authored content with optional media, privacy, optional share-of-another-post (`SharedPostId`) |
| **`Comment`** | `BaseEntity<int>` | `int` | Comment on a post; supports threaded replies via `ParentCommentId` |
| **`PostLikes`** | *(none)* | Composite `(PostId, UserId)` | Join entity for post likes; not a `BaseEntity` |
| **`Friendship`** | *(none)* | Composite `(RequesterId, ReceiverId)` | Friend request graph edge with status lifecycle |
| **`Message`** | `BaseEntity<int>` | `int` | Direct message between two users; optional shared post reference; read receipts |
| **`Notification`** | `BaseEntity<int>` | `int` | In-app notification with type, message, target URL, read flag |
| **`RefreshToken`** | `BaseEntity<int>` | `int` | Opaque refresh token stored in DB; revocation and expiry |
| **`EmailOtp`** | `BaseEntity<int>` | `int` | Hashed OTP records for registration, password reset, and email change |

**`ApplicationUser`** (excerpt):

```7:37:Domain/Entites/ApplicationUser.cs
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;
        // ...
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Friendship> SentFriendRequests { get; set; } = new List<Friendship>();
        // ... Comments, PostLikes, Messages, Notifications
    }
```

**`RefreshToken`** computed properties:

```12:14:Domain/Entites/RefreshToken.cs
        public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
        public DateTime? RevokedOn { get; set; }
        public bool IsActive => RevokedOn is null && !IsExpired;
```

**`EmailOtp`** — binds OTP to user, purpose, and optionally a target email for email-change flows:

```6:22:Domain/Entites/EmailOtp.cs
    public class EmailOtp : BaseEntity<int>
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? TargetEmail { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        public OtpPurpose Purpose { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AttemptCount { get; set; }
        public bool IsUsed { get; set; }
    }
```

#### Enums

| Enum | Values | Usage |
|------|--------|-------|
| **`OtpPurpose`** | `Registration`, `ForgotPassword`, `EmailChange` | Scopes OTP records and hash inputs |
| **`FriendShipStatus`** | `Pending`, `Accepted`, `Rejected`, `Blocked` | Friend request lifecycle |
| **`PostPrivacy`** | `Public`, `OnlyMe`, `FriendsOnly` | Feed visibility rules |
| **`NotificationType`** | `FriendRequest`, `Tag`, `PostInteraction`, `BirthdayReminder`, `MessageRequest`, `NewMessage` | Notification categorization |

#### Value Objects

There are **no explicit value object types** (no `record` wrappers for Email, Money, etc.). Normalization helpers live in `Application/Common/` (`EmailAddressValidator`, `UsernameValidator`, `OtpHasher`).

---

### 2.2 Application Layer (`Application/`)

#### Contracts & Interfaces

**Unit of Work**

```6:13:Application/Interfaces/unitofwork/IUnitOfWork.cs
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<T, TKey> Repository<T, TKey>() where T : BaseEntity<TKey>;
        IPostRepository Posts { get; }
        IFriendshipRepository Friendships { get; }
        IRefreshTokenRepository RefreshTokens { get; }
        Task<int> CompleteAsync(CancellationToken cancellationToken = default);
    }
```

**Generic Repository**

```9:17:Application/Interfaces/Repositries/IGenericRepository.cs
    public interface IGenericRepository<T, TKey> where T : BaseEntity<TKey>
    {
        Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        void Update(T entity);
        void Delete(T entity);
    }
```

**Service interfaces** (registered in `Application/DependencyInjection.cs`):

| Interface | Implementation |
|-----------|----------------|
| `IUnitOfWork` | `UnitOfWork` |
| `ITokenService` | `TokenService` |
| `IAuthService` | `AuthService` |
| `IEmailOtpService` | `EmailOtpService` |
| `IFriendshipService` | `FriendshipService` |
| `IPostService` | `PostService` |
| `IChatService` | `ChatService` |
| `INotificationService` | `NotificationService` |
| `INotificationDispatcher` | `NotificationDispatcher` |
| `ISearchService` | `SearchService` |
| `IRealtimeNotificationService` | `RealtimeNotificationService` |
| `IRealtimeChatService` | `RealtimeChatService` |
| `IFileStorageService` | `LocalFileStorageService` |
| `IEmailSender` | `SmtpEmailSender` (Singleton) |

**Specialized repositories:**

- `IPostRepository` — feed queries, includes, share-reference cleanup
- `IFriendshipRepository` — bidirectional friendship lookup, accepted friend IDs
- `IRefreshTokenRepository` — token lookup with user navigation

**Cross-cutting abstractions:**

| Abstraction | Role |
|-------------|------|
| `IEmailSender` | SMTP email dispatch (`SendAsync`) |
| `INotificationDispatcher` | Persist + push notifications |
| `IRealtimeNotificationService` / `IRealtimeChatService` | SignalR push from services |
| `IFileStorageService` | Local media upload/delete |

**Not present:** `ICurrentUserService`. The authenticated user ID is resolved in the Presentation layer (`ApiController.CurrentUserId`) and SignalR hubs (`ClaimTypes.NameIdentifier`).

#### DTO Structure

DTOs are grouped by feature under `Application/DTOs/`:

| Folder | DTOs | Direction |
|--------|------|-----------|
| `Auth/` | `RegisterRequestDto`, `LoginRequestDto`, `AuthResponseDto`, `VerifyEmailOtpDto`, `ForgotPasswordRequestDto`, `ResetPasswordRequestDto`, `RefreshTokenRequestDto`, `UpdateProfileDto`, `UserProfileDto`, `RequestChangeEmailDto`, `VerifyChangeEmailDto`, … | Request + Response |
| `PostsDtos/` | `CreatePostDto`, `PostDto`, `CreateCommentDto`, `CommentDto`, `SharePostToFeedDto`, `SharePostToChatDto`, … | Request + Response |
| `MessageDtos/` | `SendMessageDto`, `MessageDto`, `ConversationSummaryDto`, `ConversationContextDto` | Request + Response |
| `FriendshipDtos/` | `SendFriendRequestDto`, `RespondFriendRequestDto`, `FriendshipResponseDto`, `FriendSummaryDto`, `FriendBirthdayDto` | Request + Response |
| `NotificationDtos/` | `NotificationDto` | Response |
| `SearchDtos/` | `SearchResultDto` | Response |

**Internal service result type:** `Application.Common.Result<T>` wraps success/failure before controllers convert to `SuccessResponse<T>` via `ToSuccessResponse()`.

#### Application Services (summary)

| Service | Responsibility |
|---------|----------------|
| `AuthService` | Registration, login, JWT issuance, profile CRUD, email change, password reset |
| `EmailOtpService` | OTP generation, hashing, validation, cooldown, SMTP delivery |
| `TokenService` | JWT access token + refresh token generation |
| `PostService` | Posts, likes, comments, sharing, feed with privacy |
| `ChatService` | Direct messages, message requests, read receipts, conversation summaries |
| `FriendshipService` | Friend requests, accept/reject, friend lists, birthdays |
| `NotificationService` | Read/mark-all notification queries |
| `NotificationDispatcher` | Create notification row + SignalR push + unread count |
| `SearchService` | User search and discover pagination |
| `SmtpEmailSender` | `System.Net.Mail.SmtpClient` implementation |
| `LocalFileStorageService` | Saves files under `wwwroot/uploads/` with size/type validation |
| `RealtimeNotificationService` | `IHubContext<NotificationHub>` wrapper |
| `RealtimeChatService` | `IHubContext<ChatHub>` wrapper |

#### Background Jobs

`BirthdayNotificationWorker` (`IHostedService`) runs daily at **08:00 UTC**, finds users whose birthday is today, and dispatches `BirthdayReminder` notifications to accepted friends (with duplicate suppression per day).

---

### 2.3 Infrastructure Layer (`Infrastructure/`)

#### DbContext

`ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>`:

```20:27:Infrastructure/Context/ApplicationDbContext.cs
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<PostLikes> PostLikes => Set<PostLikes>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();
```

**Automatic auditing** in `SaveChangesAsync`:

- Sets `CreatedAt` on `BaseEntity<>` inserts
- Converts hard deletes to **soft deletes** (`IsDeleted = true`)

**Fluent API configurations** (via `ApplyConfigurationsFromAssembly`):

| Configuration class | Highlights |
|---------------------|------------|
| `PostConfiguration` | Soft-delete query filter; index on `(UserId, CreatedAt)`; cascade delete from user |
| `FriendshipConfiguration` | Composite PK `(RequesterId, ReceiverId)`; `Restrict` on user FKs |
| `EmailOtpConfiguration` | Indexes on `(UserId, Purpose, Email/TargetEmail, IsUsed)` and `ExpiresAt` |
| `CommentConfiguration`, `MessageConfiguration`, `NotificationConfiguration`, `PostLikeConfiguration`, `RefreshTokenConfiguration`, `ApplicationUserConfiguration` | Column lengths, relationships, indexes |

Example — post soft delete:

```25:26:Infrastructure/Configuration/PostConfiguration.cs
            builder.HasQueryFilter(p => !p.IsDeleted);
```

#### Repository & Unit of Work Implementations

Located in `Application/Repositories/` (not Infrastructure):

**`GenericRepository<T,TKey>`** — thin EF wrapper; **no** `SaveChangesAsync`:

```26:34:Application/Repositories/GenericRepository.cs
        public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

        public async Task AddAsync(T entity, CancellationToken ct = default)
            => await _dbSet.AddAsync(entity, ct);

        public void Update(T entity) => _dbSet.Update(entity);
        public void Delete(T entity) => _dbSet.Remove(entity);
```

**`PostRepository`** adds eager-loading queries with `AsSplitQuery()` and `.Include()` chains for feed/detail views.

**`UnitOfWork`** — lazy generic repo cache + specialized repos; **`CompleteAsync`** delegates to `_context.SaveChangesAsync`:

```35:36:Application/Repositories/UnitOfWork.cs
        public async Task<int> CompleteAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);
```

**Transaction note:** There is **no** `BeginTransactionAsync` / `CommitTransactionAsync` / `RollbackTransactionAsync` on `IUnitOfWork`. Multi-step operations rely on EF Core's implicit per-`SaveChangesAsync` transaction. Explicit distributed transactions are not implemented.

#### Infrastructure DI (`Infrastructure/DependencyInjection.cs`)

Registers:

1. **JWT Bearer** authentication with SignalR query-string token support
2. **MvcCookie** authentication scheme for Razor pages
3. **Authorization** (default policies)
4. **DbContext** → SQL Server
5. **IdentityCore** with password rules and EF stores
6. **Options binding:** `JwtOptions`, `AppOptions`, `SmtpSettings`, `OtpOptions`

JWT + SignalR token extraction:

```62:81:Infrastructure/DependencyInjection.cs
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!string.IsNullOrWhiteSpace(accessToken) &&
                                (path.StartsWithSegments("/chatHub") ||
                                 path.StartsWithSegments("/notificationHub") ||
                                 path.StartsWithSegments("/hubs/chat") ||
                                 path.StartsWithSegments("/hubs/notifications")))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
```

#### Migrations

Managed in `Infrastructure/Migrations/`. Applied automatically at startup:

```76:84:Presentation/Program.cs
        var context = services.GetRequiredService<ApplicationDbContext>();
        if (context.Database.IsSqlServer())
        {
            // ...
            await context.Database.MigrateAsync();
        }
```

Notable migrations: initial schema, email OTP sync, shared post on messages, comment replies, username uniqueness, pending model sync.

---

### 2.4 Presentation Layer (`Presentation/`)

#### Program Entry & Middleware Pipeline

```16:140:Presentation/Program.cs
var builder = WebApplication.CreateBuilder(args);
// ...
builder.Services.AddControllersWithViews() /* + InvalidModelStateResponseFactory → ErrorResponse */;
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSignalR().AddJsonProtocol(/* camelCase */);
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<BirthdayNotificationWorker>();

// ... migrate DB, LegacyUsernameRepair ...

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles(/* video MIME types */);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.MapStaticAssets(); }

app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<ChatHub>("/chatHub");
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Feed}/{action=Index}/{id?}");
```

**Pipeline order:**

1. Exception handler (`GlobalExceptionHandler`)
2. HTTPS redirection (non-Development)
3. Static files (`wwwroot`, including uploads)
4. Routing
5. Authentication → Authorization
6. HSTS + static assets (Production)
7. SignalR hubs, API controllers, MVC default route

#### Controller Organization

| Type | Location | Purpose |
|------|----------|---------|
| **API controllers** | `Presentation/Controllers/*.cs` | JSON REST under `/api/[controller]` |
| **MVC controllers** | `Presentation/Controllers/Mvc/*.cs` | Razor views: `Feed`, `Profile`, `Chat`, `Auth`, `Friendships`, `Notifications`, `Search`, `Home` |
| **Base API controller** | `ApiController` | Provides `CurrentUserId` from JWT/cookie claims |

**Response pattern (API):** Services return `Result<T>` → controllers call `.ToSuccessResponse()` → HTTP 200 with `SuccessResponse<T>`. Errors throw `AppException` subclasses → `GlobalExceptionHandler` → `ErrorResponse` JSON.

**Model validation:** Invalid `[ApiController]` model state returns `ErrorResponse` with `fieldErrors` (camelCase keys).

#### Global Exception Handler

Maps exception types to HTTP status codes:

| Exception | Status |
|-----------|--------|
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| Unhandled | 500 |

Supports `FieldErrors` dictionary and optional `Details` payload (e.g., `{ pendingEmail }` on unverified login).

---

## 3. Core Architectural Patterns & Design Approaches

### 3.1 Repository & Unit of Work Pattern

**Motivation:** Decouple application services from EF Core APIs; centralize persistence commits; allow specialized query methods on feature repositories.

**Why `SaveChangesAsync` is not on `IGenericRepository`:**

Repositories perform **track/add/update/remove** only. A single `IUnitOfWork.CompleteAsync()` commits all pending changes in one EF Core unit of work. This prevents partial commits when a service performs multiple repository operations (e.g., add message + add notification) before calling `CompleteAsync` once.

**Transaction handling (actual behavior):**

- Each `CompleteAsync()` call maps to one `SaveChangesAsync()`, which EF wraps in an implicit database transaction.
- **No explicit** `BeginTransactionAsync` / `CommitTransactionAsync` / `RollbackTransactionAsync` exists on `IUnitOfWork`.
- Multi-table workflows (e.g., `NotificationDispatcher.DispatchAsync`) call `CompleteAsync` after notification insert; realtime push happens after commit.
- Some flows call `CompleteAsync` multiple times sequentially (e.g., post creation notifies friends in a loop with a second `CompleteAsync` at the end) — each call is its own transaction, not one atomic batch.

**Expression-based queries:**

`FindAsync(Expression<Func<T, bool>> predicate)` keeps filtering in the repository layer using LINQ expressions translated to SQL. Complex graph loading (includes, paging, privacy filters) lives in specialized repositories like `PostRepository.GetFeedPostsAsync`:

```40:51:Application/Repositories/PostRepository.cs
            var query = _dbSet.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.User)
                // ...
                .Where(p =>
                    p.UserId == currentUserId ||
                    p.Privacy == PostPrivacy.Public ||
                    (p.Privacy == PostPrivacy.FriendsOnly && friendList.Contains(p.UserId)))
                .OrderByDescending(p => p.CreatedAt);
```

### 3.2 Current User Identity Abstraction

**Pattern used:** Claim extraction at the **Presentation/Hub boundary**, not a dedicated `ICurrentUserService`.

**API controllers:**

```13:22:Presentation/Controllers/ApiController.cs
        protected string CurrentUserId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedException("User is not authenticated.");
                return userId;
            }
        }
```

**SignalR hubs** duplicate the same claim read:

```21:23:Application/Hubs/ChatHub.cs
    private string CurrentUserId =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("User identity not found.");
```

**JWT claims issued** (`TokenService.GenerateAccessToken`):

- `ClaimTypes.NameIdentifier` → user Id
- `ClaimTypes.Email`
- `ClaimTypes.Name` → display name
- `ClaimTypes.Role` (per Identity role)
- `JwtRegisteredClaimNames.Jti` → unique token id

Services receive `userId` as a **method parameter** from controllers — domain and application layers stay free of `IHttpContextAccessor`.

### 3.3 Immutability & Snapshot Patterns (Social Domain)

**E-commerce price snapshotting (`CartItem` → `OrderItems.UnitPrice`) does not apply** — Connectly has no cart or order entities.

Analogous patterns in this codebase:

| Pattern | Implementation |
|---------|----------------|
| **Message content snapshot** | `Message.Content` and `SharedPostId` are stored at send time; later edits to the shared post do not rewrite historical messages |
| **Post soft delete** | Deleted posts remain in DB with `IsDeleted = true`; query filters hide them from feeds |
| **OTP code hashing** | Plain 6-digit codes never stored; `CodeHash` = SHA-256 of `code:email:targetEmail:purpose:pepper` |
| **Refresh token rotation** | Old refresh token gets `RevokedOn` set; new token issued on refresh |

### 3.4 Result + Response Envelope Pattern

Services return `Result<T>` internally. Controllers translate successful results:

```32:33:Application/Common/Result.cs
        public SuccessResponse<T> ToSuccessResponse()
            => SuccessResponse<T>.Create(Data!, Message);
```

Failures are **exception-driven** (`throw new BadRequestException(...)`) rather than `Result.Failure` in most auth/social flows.

### 3.5 Dual Authentication (JWT + MVC Cookie)

- **API / SignalR:** `Authorization: Bearer {accessToken}` (default scheme = JWT Bearer).
- **Razor navigation:** After login, client calls `POST /api/auth/mvc-session` with Bearer token → server issues HttpOnly `Connectly.Mvc.Session` cookie (`MvcCookie` scheme).
- Picture upload endpoints accept **both** schemes: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + ",MvcCookie")]`.

### 3.6 Notification Dispatch Pipeline

Decouples **persistence** from **real-time delivery**:

1. `NotificationDispatcher.DispatchAsync` skips self-notifications.
2. Inserts `Notification` entity → `CompleteAsync`.
3. Builds `NotificationDto` with actor display name/photo.
4. `IRealtimeNotificationService.PushNotificationToUserAsync` → SignalR group.
5. Re-counts unread notifications → pushes `UpdateUnreadCount`.

---

## 4. End-to-End Request Flows & Data Lifecycles

### 4.1 Authentication & Token Lifecycle

#### Registration

```
POST /api/auth/register
  → AuthController.Register
  → AuthService.RegisterAsync
      → Validate password match, username uniqueness
      → UserManager.CreateAsync (Identity password hashing)
      → EmailOtpService.SendRegistrationOtpAsync
          → Invalidate prior unused OTPs (respect cooldown)
          → Generate 6-digit code, hash with OtpHasher
          → Insert EmailOtp row → UnitOfWork.CompleteAsync
          → SmtpEmailSender.SendAsync
  → 200 SuccessResponse<RegisterPendingResponseDto>
```

If email exists but unverified, resends OTP instead of conflict.

#### Email Verification

```
POST /api/auth/verify-email { email, code }
  → EmailOtpService.ValidateRegistrationOtpAsync (expiry, attempts, hash compare)
  → user.EmailConfirmed = true; UserManager.UpdateAsync
  → IssueAuthResponseAsync (access JWT + DB refresh token)
  → 200 SuccessResponse<AuthResponseDto>
```

#### Login

```
POST /api/auth/login { login, password }
  → UserAccountLookup.FindByLoginAsync (email or username)
  → UserManager.CheckPasswordAsync
  → If !EmailConfirmed → resend OTP, 401 with pendingEmail hint
  → TokenService.GenerateAccessToken (60 min)
  → TokenService.GenerateRefreshToken (7 days)
  → RefreshTokens.AddAsync → CompleteAsync
  → 200 SuccessResponse<AuthResponseDto>
```

Access token generation:

```40:47:Application/Services/TokenService.cs
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.DurationInMinutes),
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                SigningCredentials = creds
            };
```

#### Refresh Token Rotation

```
POST /api/auth/refresh-token { refreshToken }
  → RefreshTokenRepository.GetByTokenAsync (includes User)
  → Validate IsActive
  → Set existingToken.RevokedOn = UtcNow
  → Generate new access + refresh tokens
  → Add new refresh token → CompleteAsync
  → 200 SuccessResponse<AuthResponseDto>
```

#### Revocation

- **Single token:** `POST /api/auth/revoke-token` sets `RevokedOn`.
- **All sessions:** `RevokeAllRefreshTokensAsync` after password reset or email change.

---

### 4.2 Database-Backed Email OTP Workflows

All OTP flows share `EmailOtpService.SendOtpAsync` / `ValidateOtpAsync`.

**Hash input** (prevents cross-purpose/code reuse):

```9:15:Application/Common/OtpHasher.cs
        public static string Hash(string code, string email, OtpPurpose purpose, string? targetEmail, string pepper)
        {
            var payload = $"{code.Trim()}:{normalizedEmail}:{normalizedTarget}:{(int)purpose}:{pepper}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }
```

#### Registration Confirmation

| Step | Action |
|------|--------|
| Send | Purpose = `Registration`; email = user's inbox; `TargetEmail` = null |
| Validate | Match `UserId + Purpose + Email`; mark `IsUsed` |
| Outcome | `EmailConfirmed = true`; issue tokens |

#### Password Reset

| Step | Action |
|------|--------|
| Request | `ForgotPasswordAsync` — always returns generic success message (no account enumeration) |
| Send OTP | Purpose = `ForgotPassword` to account email |
| Reset | Validate OTP → `GeneratePasswordResetTokenAsync` → `ResetPasswordAsync` → revoke all refresh tokens |

#### Secure Email Change

| Step | Action |
|------|--------|
| Request | Authenticated `POST /api/auth/change-email/request` |
| Availability | `EnsureEmailAvailableForUserAsync` — rejects current email and taken addresses |
| Send OTP | Purpose = `EmailChange`; `TargetEmail` = new address; **delivered to new email** |
| Confirm | Validate OTP bound to `UserId + TargetEmail`; `SetEmailAsync`; `EmailConfirmed = true` |
| Security | `RevokeAllRefreshTokensAsync`; courtesy alert email to **old** address |
| Race prevention | OTP queries filter `(purpose != EmailChange \|\| o.TargetEmail == normalizedTarget)`; prior unused OTPs invalidated on resend |

**Brute-force protection:** `AttemptCount` incremented on wrong code; OTP marked used after `MaxAttempts` (5).

---

### 4.3 Social Graph: Friend Requests

```
POST /api/friendships/request { receiverId }
  → FriendshipService.SendRequestAsync
      → Validate not self; receiver exists
      → GetFriendshipAsync — handle existing Pending/Accepted/Blocked/Rejected
      → Friendships.AddAsync or Update
      → NotificationDispatcher (FriendRequest) → DB + SignalR
      → CompleteAsync
```

```
POST /api/friendships/respond { requesterId, accept: bool }
  → Update Friendship.Status to Accepted or Rejected
  → Notify requester on accept
  → CompleteAsync
```

---

### 4.4 Posts, Feed & Interactions

#### Create Post

```
POST /api/posts  OR  POST /api/posts/upload (multipart)
  → PostsController
  → [upload path] LocalFileStorageService.SaveAsync → wwwroot/uploads/posts/
  → PostService.CreatePostAsync
      → Insert Post → CompleteAsync
      → If privacy != OnlyMe: notify each accepted friend (PostInteraction)
      → CompleteAsync again
```

#### Feed

```
GET /api/posts/feed?pageNumber=&pageSize=
  → PostService.GetFeedAsync
  → PostRepository.GetFeedPostsAsync
      → Public posts + own posts + FriendsOnly from accepted friends
  → Map to PostDto with like/comment counts, viewer-specific flags
```

#### Like Toggle

```
POST /api/posts/{id}/like
  → Toggle PostLikes join row
  → On new like: NotificationDispatcher to post owner
  → CompleteAsync
```

---

### 4.5 Chat & Message Requests

Non-friends may send **one** message until the recipient replies (message request model):

```
POST /api/chat/messages  OR  ChatHub.SendDirectMessage
  → ChatService.SendMessageAsync
      → Validate receiver, block status, friendship
      → If not friend and conversation not accepted:
          → Allow only one outbound message (IsRequest = true)
      → MessageRepository.AddAsync
      → NotificationDispatcher (MessageRequest or NewMessage)
      → CompleteAsync
      → RealtimeChatService.PushMessageToUserAsync (both sender and receiver groups)
```

**Conversation acceptance:** If the other user has already sent a message, the next reply from the current user accepts the thread (no longer a request).

---

## 5. SignalR Implementation & Real-Time Communications

### Hub Architecture

| Hub | Route | Client Interface | Auth |
|-----|-------|------------------|------|
| `NotificationHub` | `/notificationHub` | `INotificationClient` | `[Authorize]` |
| `ChatHub` | `/chatHub` | `IChatClient` | `[Authorize]` |

Hub classes live in **`Application/Hubs/`** (not Presentation).

### Connection Lifecycle

1. Client obtains JWT access token (login/refresh).
2. Client connects with token in query string: `/chatHub?access_token={jwt}` (WebSockets cannot set Authorization header reliably).
3. `OnConnectedAsync` adds connection to a **group named after the user's Id** (`ClaimTypes.NameIdentifier`).
4. `OnDisconnectedAsync` removes the connection from that group.

```25:28:Application/Hubs/NotificationHub.cs
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CurrentUserId);
        await base.OnConnectedAsync();
    }
```

**Transports:** ASP.NET Core SignalR negotiates WebSockets, Server-Sent Events, or Long Polling automatically.

### Client Contracts

**`INotificationClient`:**

```7:10:Application/Interfaces/Hubs/INotificationClient.cs
        Task ReceiveNotification(NotificationDto notification);
        Task UpdateUnreadCount(int unreadCount);
```

**`IChatClient`:**

```7:11:Application/Interfaces/Hubs/IChatClient.cs
        Task ReceiveMessage(MessageDto message);
        Task MessageRead(int messageId, string readerId);
        Task UserTyping(string senderId);
        Task UserStoppedTyping(string senderId);
```

### Targeting & Groups

| Scenario | Targeting |
|----------|-----------|
| New notification | `Clients.Group(recipientUserId).ReceiveNotification(...)` |
| Unread count update | `Clients.Group(userId).UpdateUnreadCount(count)` |
| New chat message | `Clients.Group(receiverId)` and `Clients.Group(senderId)` |
| Typing indicator | `Clients.Group(receiverId).UserTyping(senderId)` |

Implemented via `IHubContext<THub, TClient>` in `RealtimeNotificationService` and `RealtimeChatService` — services do not reference hub instances directly during HTTP requests.

### Hub Methods (client → server)

| Hub | Method | Purpose |
|-----|--------|---------|
| `ChatHub` | `SendDirectMessage(SendMessageDto)` | Delegates to `IChatService.SendMessageAsync` |
| `ChatHub` | `SendTypingIndicator(receiverId)` | Push typing state to receiver's group |
| `ChatHub` | `SendStoppedTypingIndicator(receiverId)` | Clear typing state |
| `NotificationHub` | *(none)* | Receive-only hub; server pushes via `IHubContext` |

---

## 6. API Catalog & Endpoint Directory

All routes below are relative to the application base URL (e.g. `https://localhost:7242`).

**Default auth:** `[Authorize]` on controller = JWT Bearer required unless noted.

**Success response shape:**

```json
{
  "success": true,
  "message": "...",
  "data": { /* T */ }
}
```

### Auth — `/api/auth`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| POST | `/api/auth/register` | No | `RegisterRequestDto` | `RegisterPendingResponseDto` | Create account; send registration OTP |
| POST | `/api/auth/verify-email` | No | `VerifyEmailOtpDto` | `AuthResponseDto` | Confirm email; issue tokens |
| POST | `/api/auth/resend-verification` | No | `ResendEmailOtpDto` | `OtpSendResponseDto` | Resend registration OTP |
| POST | `/api/auth/login` | No | `LoginRequestDto` | `AuthResponseDto` | Authenticate; issue tokens |
| POST | `/api/auth/forgot-password` | No | `ForgotPasswordRequestDto` | `ForgotPasswordResponseDto` | Request password-reset OTP |
| POST | `/api/auth/reset-password` | No | `ResetPasswordRequestDto` | `bool` | Validate OTP; set new password |
| POST | `/api/auth/refresh-token` | No | `RefreshTokenRequestDto` | `AuthResponseDto` | Rotate refresh token; new access token |
| POST | `/api/auth/revoke-token` | Yes | `RevokeTokenRequestDto` | `bool` | Revoke a refresh token |
| POST | `/api/auth/mvc-session` | Yes | — | *(204 No Content)* | Exchange Bearer for MVC cookie |
| POST | `/api/auth/mvc-signout` | No | — | *(204 No Content)* | Clear MVC cookie |
| GET | `/api/auth/me` | Yes | — | `UserProfileDto` | Current user profile |
| GET | `/api/auth/profile/{userId}` | Yes | — | `UserProfileDto` | Any user's profile |
| PUT | `/api/auth/profile` | Yes | `UpdateProfileDto` | `UserProfileDto` | Update display name, username, bio, URLs |
| POST | `/api/auth/change-email/request` | Yes | `RequestChangeEmailDto` | `OtpSendResponseDto` | Send OTP to new email |
| POST | `/api/auth/change-email/confirm` | Yes | `VerifyChangeEmailDto` | `UserProfileDto` | Confirm email change |
| POST | `/api/auth/profile-picture` | Yes (JWT or MvcCookie) | `multipart/form-data` (`file`) | `UserProfileDto` | Upload profile image |
| POST | `/api/auth/cover-picture` | Yes (JWT or MvcCookie) | `multipart/form-data` (`file`) | `UserProfileDto` | Upload cover image |

### Posts — `/api/posts`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| POST | `/api/posts` | Yes | `CreatePostDto` | `PostDto` | Create text/media post |
| POST | `/api/posts/upload` | Yes | `multipart/form-data` | `PostDto` | Create post with file upload |
| GET | `/api/posts/{id}` | Yes | — | `PostDto` | Get single post |
| GET | `/api/posts/feed` | Yes | Query: `pageNumber`, `pageSize` | `PagedResult<PostDto>` | Personalized feed |
| DELETE | `/api/posts/{id}` | Yes | — | `bool` | Soft-delete own post |
| POST | `/api/posts/{id}/like` | Yes | — | `bool` (liked state) | Toggle like |
| GET | `/api/posts/{id}/comments` | Yes | — | `IReadOnlyList<CommentDto>` | List comments |
| GET | `/api/posts/{id}/likes` | Yes | — | `IReadOnlyList<PostLikeUserDto>` | List users who liked |
| POST | `/api/posts/{id}/comments` | Yes | `CreateCommentDto` | `CommentDto` | Add comment |
| DELETE | `/api/posts/{id}/comments/{commentId}` | Yes | — | `bool` | Delete own comment |
| POST | `/api/posts/{id}/share/feed` | Yes | `SharePostToFeedDto` | `PostDto` | Share post to own feed |
| POST | `/api/posts/{id}/share/chat` | Yes | `SharePostToChatDto` | `MessageDto` | Share post via DM |

### Chat — `/api/chat`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| POST | `/api/chat/messages` | Yes | `SendMessageDto` | `MessageDto` | Send direct message |
| GET | `/api/chat/conversations` | Yes | — | `IReadOnlyList<ConversationSummaryDto>` | Inbox summary |
| GET | `/api/chat/messages/{otherUserId}/context` | Yes | — | `ConversationContextDto` | Header/context for chat UI |
| GET | `/api/chat/messages/{otherUserId}` | Yes | — | `IReadOnlyList<MessageDto>` | Full conversation thread |
| PUT | `/api/chat/messages/{senderId}/read` | Yes | — | `bool` | Mark messages from sender as read |

### Friendships — `/api/friendships`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| POST | `/api/friendships/request` | Yes | `SendFriendRequestDto` | `FriendshipResponseDto` | Send friend request |
| POST | `/api/friendships/respond` | Yes | `RespondFriendRequestDto` | `FriendshipResponseDto` | Accept/reject request |
| GET | `/api/friendships/friends/{userId}` | Yes | — | `IReadOnlyList<FriendSummaryDto>` | List user's friends |
| GET | `/api/friendships/birthdays-today` | Yes | — | `IReadOnlyList<FriendBirthdayDto>` | Friends with birthday today |
| GET | `/api/friendships/pending` | Yes | — | `IReadOnlyList<FriendshipResponseDto>` | Incoming pending requests |

### Notifications — `/api/notifications`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| GET | `/api/notifications` | Yes | — | `IReadOnlyList<NotificationDto>` | List notifications |
| PUT | `/api/notifications/{id}/read` | Yes | — | `bool` | Mark one as read |
| PUT | `/api/notifications/read-all` | Yes | — | `bool` | Mark all as read |

### Search — `/api/search`

| Method | Route | Auth | Request DTO | Response DTO | Purpose |
|--------|-------|------|-------------|--------------|---------|
| GET | `/api/search` | Yes | Query: `q` | `SearchResultDto` | Search users/posts |
| GET | `/api/search/discover` | Yes | Query: `page`, `pageSize`, `q` | Paged user discovery | Discover people |

### SignalR Endpoints (non-REST)

| Route | Protocol | Auth | Purpose |
|-------|----------|------|---------|
| `/notificationHub` | WebSocket/SSE/LP | JWT via `?access_token=` | Push notifications |
| `/chatHub` | WebSocket/SSE/LP | JWT via `?access_token=` | Real-time chat + typing |

### MVC Routes (browser UI)

MVC controllers under `Presentation/Controllers/Mvc/` serve Razor views — not JSON API. Default route: `{controller=Feed}/{action=Index}/{id?}`.

| Controller | Example routes | Purpose |
|------------|----------------|---------|
| `FeedController` | `/Feed`, `/Feed/Index` | Main feed page |
| `ProfileController` | `/Profile?userId=` | User profile |
| `ChatController` | `/Chat?id=` | Chat UI |
| `AuthController` | `/Auth/Login`, `/Auth/Register` | Login/register pages |
| `FriendshipsController` | `/Friendships/Pending` | Friend requests UI |
| `NotificationsController` | `/Notifications` | Notifications page |
| `SearchController` | `/Search` | Search/discover UI |
| `HomeController` | `/Home` | Landing/misc |

---

## Appendix A: Configuration Reference

| Key | Required | Description |
|-----|----------|-------------|
| `ConnectionStrings:Conn` | Yes | SQL Server connection |
| `Jwt:Key` | Yes (≥32 chars prod) | HMAC-SHA256 signing key |
| `Jwt:Issuer` / `Jwt:Audience` | Yes | Token validation |
| `Jwt:DurationInMinutes` | Yes | Access token TTL (default 60) |
| `Jwt:RefreshTokenDurationInDays` | Yes | Refresh token TTL (default 7) |
| `Smtp:Host`, `Port`, `EnableSsl` | For OTP | SMTP server |
| `Smtp:SenderEmail`, `SenderPassword` | For OTP | Credentials (use app password for Gmail) |
| `Otp:Pepper` | Yes (prod) | OTP hash pepper |
| `Otp:MaxAttempts`, `ResendCooldownSeconds` | Optional | Brute-force / rate limits |
| `App:PublicUrl` | Prod | Public HTTPS base URL |

**Development secrets example:**

```powershell
dotnet user-secrets set "Jwt:Key" "<32+ char secret>" --project Presentation
dotnet user-secrets set "Smtp:SenderEmail" "you@gmail.com" --project Presentation
dotnet user-secrets set "Smtp:SenderPassword" "<app-password>" --project Presentation
dotnet user-secrets set "Otp:Pepper" "<32+ char secret>" --project Presentation
```

---

## Appendix B: Explicit Non-Features

The following are **not implemented** in this codebase (common in e-commerce templates but absent here):

- `Product`, `Cart`, `CartItem`, `Order`, `OrderItems` entities
- Checkout or payment flows
- `ICurrentUserService` / `IHttpContextAccessor` wrapper in Application layer
- Explicit database transactions on `IUnitOfWork`
- AutoMapper, Mapster, FluentValidation
- Redis, external blob storage, or message queues
- DbSeeder / demo data initialization (removed; only `LegacyUsernameRepair` runs at startup)

---

*Document generated from source analysis of the Connectly solution. For migration commands: `dotnet ef database update --project Infrastructure --startup-project Presentation`.*
