
# Deployment

Version: 1.0

## Goal

Describe how Mood Pickup System is deployed in development and production.

## Technology

- Docker Compose
- PostgreSQL
- ASP.NET Core
- React (Vite)
- Nginx (production reverse proxy)

## Containers

1. postgres
2. backend
3. frontend
4. nginx (production)

## Environment Variables

Backend:

- ASPNETCORE_ENVIRONMENT
- ConnectionStrings__DefaultConnection
- Jwt__Issuer
- Jwt__Audience
- Jwt__SigningKey
- Telegram__Enabled
- Telegram__BotToken
- Telegram__BotUsername
- Telegram__WebhookSecret
- Telegram__PublicBaseUrl
- Telegram__WebhookPath
- Telegram__RegisterWebhookOnStartup
- Telegram__DropPendingUpdatesOnRegistration
- Telegram__UseDevelopmentSender
- SignalR__Enabled
- Storage__Path

Frontend:

- VITE_API_URL
- VITE_SIGNALR_URL

## Development

Run:

docker compose up --build

Requirements:

- automatic EF Core migrations (optional flag)
- hot reload
- fake payment provider
- fake Telegram sender only when
  `Telegram__UseDevelopmentSender=true`; webhook registration disabled

## Production

Requirements:

- HTTPS only
- reverse proxy
- secure cookies
- automatic restart
- daily database backups
- centralized logs
- health checks
- real Telegram sender, public HTTPS backend URL, and automatic webhook
  registration

Real Telegram deployment example:

```text
Telegram__Enabled=true
Telegram__BotToken=<secret bot token>
Telegram__BotUsername=<bot username without @>
Telegram__WebhookSecret=<random letters/digits/underscore/hyphen>
Telegram__PublicBaseUrl=https://api.example.com
Telegram__WebhookPath=/api/v1/telegram/webhook
Telegram__RegisterWebhookOnStartup=true
Telegram__DropPendingUpdatesOnRegistration=false
Telegram__UseDevelopmentSender=false
```

`PublicBaseUrl` contains only scheme and host. The application constructs
`{PublicBaseUrl}{WebhookPath}`, validates the token and username with `getMe`,
registers the webhook and secret for `message` updates, and confirms the URL
with `getWebhookInfo`. No deployment-domain source edit or manual Telegram API
call is required.

## Health Endpoints

GET /health/live
GET /health/ready

Liveness never depends on Telegram. In real mode readiness uses the cached
startup registration result and does not contact Telegram for every probe.

## Logging

- Structured JSON logs
- Serilog
- Separate application and access logs

## Database Backups

Daily backup

Retention:
- 7 daily
- 4 weekly
- 6 monthly

## Media

Uploaded images stored outside containers using mounted volumes.

## Security Checklist

- Secrets from environment
- No secrets in Git
- HTTPS required
- Security headers enabled
- CORS restricted
- Rate limiting enabled

## Release Process

1. Build
2. Run tests
3. Create Docker images
4. Apply EF migrations
5. Deploy
6. Verify health endpoints
7. Smoke test

## Rollback

Rollback uses previous Docker image and previous database backup if schema rollback is impossible.
