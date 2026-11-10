# Frontend local integration

## Start the backend

From the repository root, copy `.env.example` to `.env`, fill in the values, then run:

```sh
docker compose up --build
```

The API listens at `http://localhost:5203`. PostgreSQL is not included in Docker Compose: each developer runs or provisions their own PostgreSQL instance, creates a database, and applies the EF Core migrations before starting the API container.

For PostgreSQL running on the same computer, set `Host=host.docker.internal` in `ConnectionStrings__DefaultConnection`. Keep the database name and credentials the same as the database they created. The API container uses this connection string to connect to the host database.

To create the local database and apply migrations from the repository root, install the .NET 10 SDK and EF CLI (`dotnet tool install --global dotnet-ef --version 10.0.14`), set the connection string environment variable for the shell, then run:

```sh
dotnet ef database update --project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj --startup-project src/Cortex.Infrastructure/Cortex.Infrastructure.csproj
```

For migrations run directly on the host, use `Host=localhost` in the connection string. In PowerShell: `$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=<database>;Username=<user>;Password=<password>"`. In bash: `export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=<database>;Username=<user>;Password=<password>'`. In `.env`, use `Host=host.docker.internal` so the API container can reach PostgreSQL on the host.

Required local configuration:

- `GOOGLE_CLIENT_ID`: OAuth client ID shared by the Google provider in NextAuth and the API audience check.
- `JWT_SECRET`: random secret of at least 32 characters for local development.
- Brevo sender credentials: used by registration and email verification.
- `FRONTEND_BASE_URL`: frontend origin, normally `http://localhost:3000`.

Do not commit `.env` or share production credentials. Each developer should use local development credentials.

## Google sign-in handoff

1. Configure NextAuth's Google provider with the same Google OAuth client ID in `.env` as `GOOGLE_CLIENT_ID`.
2. In Google Cloud Console, add `http://localhost:3000` as an authorized JavaScript origin and `http://localhost:3000/api/auth/callback/google` as an authorized redirect URI.
3. After NextAuth completes Google sign-in, obtain the provider's Google ID token (`account.id_token`). Do not use the NextAuth session token or send user profile fields as proof of identity.
4. Exchange the ID token with the Cortex API:

```http
POST http://localhost:5203/api/auth/google-session
Content-Type: application/json

{"idToken":"<Google ID token>"}
```

5. On success, Cortex responds with `accessToken`, `accessTokenExpiresAt`, `refreshToken`, `refreshTokenExpiresAt` (UTC), and `user`, and also sets `cortex_access_token` and `cortex_refresh_token` HttpOnly cookies. The JSON tokens are temporarily retained for integration testing.
6. For browser requests directly to the API, set `credentials: "include"`. CORS is configured for the exact `FRONTEND_BASE_URL` origin and allows credentials. The API reads the access token from the cookie when no bearer token is supplied.

The Google ID token must be sent to the backend over HTTPS outside local development. Keep it short-lived and do not log it. For NextAuth server-side callbacks, ensure the ID token is kept server-side rather than exposed in the public session object.

## Error format

Every error response, including 500, has the same `application/problem+json` body. Branch on the HTTP status, show `detail` to the user, and map `errors` onto form fields (the keys are camelCase).

```json
// 409
{ "title": "Conflict", "status": 409, "detail": "This email is already registered.", "traceId": "00-…" }
// 400 validation
{ "title": "One or more validation errors occurred.", "status": 400, "errors": { "email": ["'Email' is not a valid email address."] }, "traceId": "00-…" }
// 500
{ "title": "Internal Server Error", "status": 500, "detail": "An unexpected error occurred.", "traceId": "00-…" }
```

`traceId` matches the server logs, so include it in bug reports.

## Other useful endpoints

- `POST /api/auth/register`: creates an unverified account and sends a verification email. It returns `201` with the user and does not sign the user in. Errors: `409` if the email is taken.
- `POST /api/auth/resend-verification`: sends a new verification email and returns `204`. Errors: `404` if no account uses the email, `409` if it's already verified.
- `POST /api/auth/login`: credential sign-in. Errors: `401` for wrong credentials, `403` until the email is verified.
- `GET /api/auth/verify-email?token=...`: verifies the email and issues a Cortex session. Errors: `400` if the token is invalid or expired.
- `POST /api/auth/refresh`: rotates the session. The refresh token comes from the `cortex_refresh_token` cookie (send no body) or from `{"refreshToken":"..."}`. Each refresh token works **once**. Always store the new one from the response or cookies. Replaying a used token revokes the whole session, so keep only one refresh in flight per session (deduplicate concurrent refreshes in NextAuth and across tabs). A failed refresh returns `401` and clears the cookies, so send the user back to sign-in.
- `POST /api/auth/logout`: revokes the session belonging to the refresh token (from the cookie or `{"refreshToken":"..."}`) and clears the cookies. It needs no access token and always returns `204`.
- `GET /scalar`: interactive API reference in Development.

## Businesses and members

All of these need a signed-in user. The business is named by `{organizationId}` in the URL, and the backend looks up the caller's membership on every request. The role is not in the JWT, so a role change or removal takes effect on the next request.

- `POST /api/organizations`: body `{"name":"...","slug":"..."}`. `slug` is optional (3–100 chars, lowercase letters, numbers and hyphens); when omitted it's derived from the name. Returns `201` with the business, and the caller becomes its `Owner`. Errors: `409` if the slug is taken.
- `GET /api/organizations`: the caller's businesses, each with the caller's `role`. Use it for the business switcher.
- `GET /api/organizations/{organizationId}` and `PUT /api/organizations/{organizationId}` (body `{"name":"...","logoUrl":"https://..."}`).
- `GET /api/organizations/{organizationId}/members`: members with `userId`, `name`, `email`, `avatarUrl`, `role`, `joinedAt`.
- `POST /api/organizations/{organizationId}/members`: body `{"email":"...","role":"Admin"}`. The email must belong to an existing Cortex account. Errors: `404` if no account uses the email, `409` if already a member.
- `PUT /api/organizations/{organizationId}/members/{userId}`: body `{"role":"Member"}`.
- `DELETE /api/organizations/{organizationId}/members/{userId}`: removes a member, or leaves the business when `{userId}` is the caller. Returns `204`.

Roles are sent and returned as strings: `Owner`, `Admin`, `Member`. Only `Admin` and `Member` can be assigned (`400` otherwise). The owner's membership can't be changed or removed (`403`).

| | Owner | Admin | Member |
|---|---|---|---|
| View business and members | yes | yes | yes |
| Update business details | yes | yes | no |
| Add, change and remove members | yes | yes | no |

A caller who is not a member gets `404`, the same as for a business that doesn't exist. A member without the needed permission gets `403`. Hide controls by role in the UI, but treat these responses as the source of truth.
