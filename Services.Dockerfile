# Étape de construction
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0@sha256:3fcf6f1e809c0553f9feb222369f58749af314af6f063f389cbd2f913b4ad556 AS build
ARG CONFIGURATION=Release
ARG SERVICE=DnDiscordAPI
ARG PORT=8080

WORKDIR /src

# Copier tous les fichiers du projet
COPY . .

# Restaurer les dépendances
RUN dotnet restore "src/${SERVICE}/${SERVICE}.csproj"

# Construire le projet
RUN dotnet build "src/${SERVICE}/${SERVICE}.csproj" -c $CONFIGURATION -o /app/build

# Étape de publication
FROM build AS publish
ARG CONFIGURATION=Release
ARG SERVICE=DnDiscordAPI
RUN dotnet publish "src/${SERVICE}/${SERVICE}.csproj" -c $CONFIGURATION -o /app/publish /p:UseAppHost=false

# Étape finale : runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
ARG SERVICE=DnDiscordAPI
ARG PORT=8080

WORKDIR /app
EXPOSE $PORT

# Configure l'URL de l'application
ENV ASPNETCORE_URLS=http://+:${PORT}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV SCALAR_URLS=http://localhost:${PORT}

# Copier les binaires publiés
COPY --from=publish /app/publish .

# Crée le symlink
RUN ln -s "${SERVICE}.dll" docker_service_name.dll

# Change l'utilisateur (l'image aspnet a déjà 'app')
USER app

ENTRYPOINT ["dotnet", "docker_service_name.dll"]