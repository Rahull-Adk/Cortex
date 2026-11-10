# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Read `AGENTS.md` first.** It holds the product spec and the mandatory rules for this project. The most important ones:
- **Do not implement, modify source, add packages, or generate migrations unless explicitly told to.** The project is still being planned. By default, explain the design, the affected layers and the trade-offs, then wait.
- **No MediatR**, and no custom mediator, CQRS handlers or `ISender`-style abstractions. Use cases are plain application service classes resolved through DI.
- Clean Architecture is strict, and multi-tenant isolation must be enforced in the backend, never only in the frontend.

## Repository contents

This repo holds only the **ASP.NET Core (.NET 10) backend**. The Next.js/NextAuth frontend lives elsewhere. `FRONTEND_INTEGRATION.md` describes the contract the frontend uses: the Google ID-token exchange, cookies and CORS.

## Commands

Run these from the repo root. The solution file is `Cortex.slnx`.

```sh
dotnet build Cortex.slnx
dotnet run --project src/Cortex.Api            # http://localhost:5203 (launchSettings); Scalar UI at /scalar in Development
docker compose up --build                  # API only, on :5203; reads .env (copy from .env.example)
```

EF Core migrations (needs `dotnet tool install --global dotnet-ef --version 10.0.14`). Migrations live in Infrastructure, which is also the startup project (Api doesn't reference `Microsoft.EntityFrameworkCore.Design`):

```sh
dotnet ef migrations add <Name> --project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj --startup-project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj
dotnet ef database update     --project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj --startup-project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj
```

`CortexDbDesignTimeFactory` reads `ConnectionStrings__DefaultConnection` from user secrets or environment variables. It never reads appsettings. Use `Host=localhost` when running on the host and `Host=host.docker.internal` inside `.env` for the container. PostgreSQL is **not** part of docker-compose, so each developer runs their own.

Tests are xUnit with Moq and live under `test/`: `Cortex.Application.Tests` (application services, authorization, validators, domain entities) and `Cortex.Infrastructure.Tests` (JWT and BCrypt adapters). Run them with `dotnet test Cortex.slnx`. Tests marked `Skip` describe rules that are not implemented yet; the reason is in the attribute. There is no lint configuration yet.

CI/CD is `.github/workflows/ci-cd.yml`: pull requests and pushes to `main` restore, build in Release with warnings as errors, run `dotnet test`, and build the Docker image. Pushes to `main` also publish the image to `ghcr.io/<owner>/<repo>` (`latest` and `sha-<short>`). There is no deploy step yet.

## Configuration

Settings are bound from configuration sections. Locally they come from user secrets (the API and Infrastructure share `UserSecretsId`) or from env vars using the `Section__Key` form:
`ConnectionStrings:DefaultConnection`, `Jwt` (Secret, Issuer, Audience, AccessTokenExpiryMinutes, RefreshTokenExpiryDays), `GoogleAuth:ClientId`, `Brevo` (ApiKey, SenderEmail, SenderName), `Frontend:BaseUrl` (the only allowed CORS origin, and the base for links in verification emails). `appsettings.json` contains no secrets.

## Architecture

Four projects under `src/`. Test projects go under `test/`. Dependencies point inward: `Api → Application, Infrastructure`, `Infrastructure → Application, Domain`, `Application → Domain`.

- **Domain**: entities with private setters, private constructors and static factories (`User.Create`, `UserAccount.CreateCredentials/CreateOAuth`). State changes go through methods such as `MarkEmailVerified`. The project has no package references. Tenancy is modeled as `Organization` plus `OrganizationMembership` (with `OrganizationRole`). A `User` is a business-owner identity. A `UserAccount` is one login method per provider (`"credentials"` stores a password hash, `"google"` stores the provider account id).
- **Application**: use-case services organized by feature folder (`Auth/AuthService.cs`, `Auth/Dtos`, `Auth/Validators`, `Auth/AuthErrors.cs`). Ports live in `Interfaces/`: repositories, `IUnitOfWork`, `ITokenService`, `IPasswordHasher`, `IEmailService`, `IExternalIdentityTokenValidator`. For expected failures, services return `Common/Result` or `Result<T>` and do not throw. A failure carries an `Error(ErrorType, Message)`, where `ErrorType` is one of Validation/Unauthorized/Forbidden/NotFound/Conflict. Errors are predefined per feature, for example `AuthErrors.EmailAlreadyRegistered`. Implicit conversions let a service simply `return AuthErrors.X;` or `return dto;`. The Application layer never references HTTP status codes. `AddApplication()` registers the services and scans the assembly for FluentValidation validators.
- **Infrastructure**: `CortexDbContext` (Npgsql) applies every `IEntityTypeConfiguration` in `Persistence/Configurations` automatically. Repositories only stage changes, and `IUnitOfWork.SaveChangesAsync` commits them. Adapters live under `Auth/`: JWT, BCrypt, Google ID-token validation, and Brevo over a named `HttpClient("Brevo")`. Strongly typed settings classes are in `Auth/Settings`. Everything is wired in `AddInfrastructure(configuration)`.
- **Api**: the composition root is `Program.cs`. Controllers stay thin and inherit `ApiControllerBase`. They return `FromResult(result, onSuccess)`. `ApiControllerBase.Problem(Error)` is the **only** place that maps `ErrorType` to a status code (400/401/403/404/409), so don't add per-controller switches. Every error body is a trimmed ProblemDetails, `{ title, status, detail?, errors?, traceId }`, configured in `Program.cs` through `AddProblemDetails`, which removes `type` and `instance`. `UseStatusCodePages` gives empty 401/404 responses the same body. Unhandled exceptions go to `GlobalExceptionHandler`, which logs the full exception and returns a short 500. Validation is global: `ValidationFilter` runs any registered `IValidator<T>` against action arguments and returns a 400 with camelCase `errors` keys. Controllers should not validate by hand.

### Organizations and authorization
- Business-scoped endpoints take `{organizationId}` in the route (`/api/organizations/{organizationId}/...`). The ID only names the target. Every business-scoped service method first calls `OrganizationAuthorizer.AuthorizeAsync(userId, organizationId, Permission)`, which loads the caller's membership from the database. No business or role is ever put in the JWT.
- A non-member (or an inactive business or user) gets `NotFound`. A member without the permission gets `Forbidden`.
- The role → permission matrix lives only in `Application/Authorization/RolePermissions.cs`. Add new `Permission` values there with the feature that needs them.
- `Organization.Create(name, slug, ownerUserId)` adds the Owner membership itself, so both rows are saved together. The Owner membership cannot be changed or removed through member endpoints, and only Admin and Member are assignable (no ownership transfer yet).
- `UnitOfWork` turns PostgreSQL unique violations into `UniqueConstraintException`. Services catch it for slug and duplicate-member races.
- Enums are serialized as strings (`JsonStringEnumConverter` in `Program.cs`).
- Subscriptions, plan limits and billing are not built yet.

### Auth flow (business owners only)
- The API issues a short-lived access JWT and an opaque refresh token. It sets both as HttpOnly cookies (`cortex_access_token`, `cortex_refresh_token`) and, for now, also returns them in the JSON body.
- Refresh tokens live in `refresh_tokens`, which stores only their SHA-256 hash. Each sign-in starts a token family. `/refresh` rotates the token, and replaying a token that was already rotated revokes the whole family. Single use is guaranteed by an `xmin` concurrency token, and `UnitOfWork` turns EF concurrency failures into `ConcurrencyConflictException`. `/refresh` and `/logout` read the token from the body or the cookie, and `/logout` needs no access token. JWT bearer auth falls back to the `cortex_access_token` cookie when no `Authorization` header is present. `MapInboundClaims = false` is set on JwtBearer, so claims keep their JWT names. Read the user id from `JwtRegisteredClaimNames.Sub`. Clearing `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap` does nothing since .NET 8, because JwtBearer now uses `JsonWebTokenHandler`.
- Email/password registration creates an unverified user and emails a verification token. The token is stored hashed and expires after 24h. Login returns 403 until the email is verified. If Brevo fails to send, the failure is logged and registration still succeeds, so the user can call resend-verification. The verify-email endpoint issues the session.
- For Google sign-in, the frontend sends Google's ID token to `POST /api/auth/google-session`, and the backend validates it against `GoogleAuth:ClientId`. The backend never trusts profile fields sent by the client.
- Storefront customers (chat widget visitors) are a separate identity type and must never go through this auth flow.