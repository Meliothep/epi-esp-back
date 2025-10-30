# Backend for DNDiscord project 

## Setup Seq for logging

```sh
docker run --name seq -d --restart unless-stopped -e ACCEPT_EULA=Y -e SEQ_FIRSTRUN_ADMINPASSWORD=secured -v seqdata:/data -p 5341:80 --network=seq-net datalust/seq 
```

## Run test 

```sh
dotnet test
```

## Run project with docker 

```sh
docker compose up 
```

## Run debug

**Debug db setup :**

```sh
docker run -d --name dndiscordPostgresDebug -e POSTGRES_USER=DnDiscord -e POSTGRES_PASSWORD=DnDiscordSecured -e POSTGRES_DB=DnDiscordDB -e PGDATA=/data/postgres -v local_pgdata:/data/postgres -p 5432:5432 postgres:16
```


**Run Project :**

```sh
dotnet run --project .\src\DnDiscordAPI\DnDiscordAPI.csproj
```

