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
dotnet ef database update --project Cortex.Infrastructure/Cortex.Infrastructure.csproj --startup-project Cortex.Api/Cortex.Api.csproj
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

5. On success, Cortex responds with `accessToken`, `refreshToken`, and `user`, and also sets `cortex_access_token` and `cortex_refresh_token` HttpOnly cookies. The JSON tokens are temporarily retained for integration testing.
6. For browser requests directly to the API, set `credentials: "include"`. CORS is configured for the exact `FRONTEND_BASE_URL` origin and allows credentials. The API reads the access token from the cookie when no bearer token is supplied.

The Google ID token must be sent to the backend over HTTPS outside local development. Keep it short-lived and do not log it. For NextAuth server-side callbacks, ensure the ID token is kept server-side rather than exposed in the public session object.

## Other useful endpoints

- `POST /api/auth/register`: creates an unverified account and sends a verification email; it does not sign the user in.
- `POST /api/auth/login`: credential sign-in; returns `403` until the email is verified.
- `GET /api/auth/verify-email?token=...`: verifies the email and issues a Cortex session.
- `POST /api/auth/refresh`: refreshes the Cortex session. The current endpoint expects `refreshToken` in the JSON body during testing.
- `POST /api/auth/logout`: clears the auth cookies.
- `GET /scalar`: interactive API reference in Development.
