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
- `PUT /api/organizations/{organizationId}/members/{userId}`: body `{"role":"Member"}`.
- `DELETE /api/organizations/{organizationId}/members/{userId}`: removes a member, or leaves the business when `{userId}` is the caller. Returns `204`.

Roles are sent and returned as strings: `Owner`, `Admin`, `Member`. Only `Admin` and `Member` can be assigned (`400` otherwise). The owner's membership can't be changed or removed (`403`).

| | Owner | Admin | Member |
|---|---|---|---|
| View business and members | yes | yes | yes |
| Update business details | yes | yes | no |
| Invite, change and remove members | yes | yes | no |

A caller who is not a member gets `404`, the same as for a business that doesn't exist. A member without the needed permission gets `403`. Hide controls by role in the UI, but treat these responses as the source of truth.

## Inviting members

Nobody is added to a business directly. An owner or admin invites an email address, Cortex emails a link, and the person becomes a member only after accepting it. The invited person does not need a Cortex account when the invitation is sent.

- `POST /api/organizations/{organizationId}/invitations`: body `{"email":"...","role":"Member"}`. Sends the email and returns `201` with `id`, `email`, `role`, `invitedByName`, `createdAt`, `expiresAt`. Inviting the same email again sends a fresh link and cancels the old one. Errors: `409` if the email already belongs to a member.
- `GET /api/organizations/{organizationId}/invitations`: invitations that have not been accepted yet. Compare `expiresAt` with the current time to label expired ones.
- `DELETE /api/organizations/{organizationId}/invitations/{invitationId}`: cancels an invitation. Returns `204`, and the emailed link stops working.
- `GET /api/invitations/preview?token=...`: public. Returns `organizationName`, `organizationLogoUrl`, `email`, `role`, `invitedByName`, `expiresAt` so the accept page can say who invited whom. Errors: `400` if the link is invalid, expired or cancelled.
- `POST /api/invitations/accept`: body `{"token":"..."}`, signed-in user. Adds the caller to the business and returns it (same shape as `GET /api/organizations/{organizationId}`). Errors: `400` if the link is invalid, expired or already used, `403` if the caller is signed in with a different email than the one invited.

The email links to `/invitations/accept?token=...` on the frontend. That page should:

1. Call the preview endpoint and show the business, the inviter and the invited email.
2. If nobody is signed in, send the person to sign in or register with the invited email, keeping the token, then come back.
3. On "Accept", call the accept endpoint and open the returned business.

Links last 7 days and work once. Only owners and admins can invite, list and cancel (`403` for members).

## Plans and subscriptions

Creating a business and managing members is free. Knowledge uploads, ingestion and AI answers need a paid subscription. Billing runs on Stripe: the backend returns a Stripe URL, the frontend redirects to it, and Stripe webhooks update the subscription. There is no "confirm payment" call to make from the frontend.

- `GET /api/plans`: public plan catalog, in display order. Each plan has `code`, `name`, `priceMinor` (satang, so `99000` is ฿990), `currency`, `aiAnswersPerPeriod`, `maxDocuments`, `maxStorageBytes`.
- `GET /api/organizations/{organizationId}/subscription`: the current plan, `status`, `hasPaidAccess`, `currentPeriodStart`, `currentPeriodEnd`, `paidThroughUtc`, `cancelAtPeriodEnd` and `usage` (`aiAnswersUsed`, `documentCount`, `storageBytes`). A business that never subscribed gets the `free` plan with `status: null`.
- `POST /api/organizations/{organizationId}/subscription/checkout`: body `{"planCode":"starter"}`. Returns `{"url":"..."}` for Stripe Checkout. Errors: `404` for an unknown plan, `400` for the free plan, `409` if the business already has a subscription (send the owner to the portal instead).
- `POST /api/organizations/{organizationId}/subscription/portal`: returns `{"url":"..."}` for the Stripe customer portal, where the owner switches plan, cancels or updates the card. Errors: `404` if the business has never checked out.

Stripe sends the user back to `/organizations/{organizationId}/billing?checkout=success` or `?checkout=cancelled`, and the portal returns to `/organizations/{organizationId}/billing`. These paths are configurable on the backend (`Stripe:CheckoutSuccessPath`, `Stripe:CheckoutCancelPath`, `Stripe:PortalReturnPath`). After a successful checkout the webhook can land a moment after the redirect, so poll the subscription endpoint until `hasPaidAccess` is `true`.

`status` is one of `Pending`, `Active`, `PastDue`, `Cancelled`, `Expired`. Use `hasPaidAccess` to decide what to unlock, not `status`: a cancelled or past-due subscription keeps access until `paidThroughUtc`.

| | Owner | Admin | Member |
|---|---|---|---|
| View subscription and usage | yes | yes | no |
| Start checkout, open portal | yes | no | no |

Paid features answer `402` when they are locked, with a `code` in the problem details:

```json
{ "title": "Payment Required", "status": 402, "detail": "An active subscription is required for this feature.", "code": "subscription_required" }
```

- `subscription_required`: no paid access. Show the upgrade prompt.
- `quota_exceeded`: the plan's AI answer allowance, document limit or storage limit is used up. Only that operation is paused; the workspace and existing knowledge stay available. AI answers reset at the next billing period.

### Local Stripe setup

Set `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__Prices__starter` and `Stripe__Prices__growth` (see `.env.example`). Create two monthly THB prices in Stripe test mode and enable plan switching in the customer portal settings. Forward webhooks with `stripe listen --forward-to localhost:5203/api/webhooks/stripe` and use the signing secret it prints. The endpoint needs `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.paid` and `invoice.payment_failed`.
