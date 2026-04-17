# Backend for DNDiscord project

.NET 9 backend with Campaign, Games, Auth, and Multiplayer modules. PostgreSQL via EF Core.

## Setup Seq for logging

```sh
docker run --name seq -d --restart unless-stopped -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_ADMINPASSWORD=secured -v seqdata:/data -p 5341:80 --network=seq-net datalust/seq
```

## Run debug

**Debug db setup:**

```sh
docker run -d --name dndiscordPostgresDebug -e POSTGRES_USER=DnDiscord -e POSTGRES_PASSWORD=DnDiscordSecured -e POSTGRES_DB=DnDiscordDB -e PGDATA=/data/postgres -v local_pgdata:/data/postgres -p 5432:5432 postgres:16
```

**Run project:**

```sh
dotnet run --project .\src\DnDiscordAPI\DnDiscordAPI.csproj
```

## Run with Docker

```sh
docker compose up
```

## Tests

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) -- Docker must be running.

```sh
dotnet test
```

- Tests spin up a temporary PostgreSQL 16 container automatically (no manual DB setup needed)
- A `TestAuthHandler` fakes Discord JWT authentication via the `X-Test-UserId` header
- `CreateAuthenticatedClient()` provides an authenticated HttpClient for tests requiring user context
- Tests are **not parallelized** (single shared Testcontainer per collection)

## CI/CD

- **CI:** GitHub Actions runs on every push/PR to `main` and `dev` -- restore, build, test (`.github/workflows/ci.yml`)
- **CD:** Dokploy auto-deploys on push to configured branches via GitHub App

