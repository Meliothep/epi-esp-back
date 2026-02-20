# Utilise une image de base ASP.NET pour l'exécution
FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:3fcf6f1e809c0553f9feb222369f58749af314af6f063f389cbd2f913b4ad556 AS base
ARG CONFIGURATION=Release
ARG SERVICE=DnDiscordAPI
ARG PORT=8080

WORKDIR /app
EXPOSE $PORT

# Configure l'URL de l'application
ENV ASPNETCORE_URLS=http://+:$PORT
ENV SCALAR_URLS=http://localhost:$PORT

# Change l'utilisateur pour éviter les permissions root
USER app

# Étape de construction
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0@sha256:3fcf6f1e809c0553f9feb222369f58749af314af6f063f389cbd2f913b4ad556 AS build
ARG CONFIGURATION=Release
ARG SERVICE=DnDiscordAPI
ARG PORT=8080

WORKDIR /src

# Copier tous les fichiers du projet
COPY . .

# Restaurer les dépendances - chemin corrigé
RUN dotnet restore "src/${SERVICE}/${SERVICE}.csproj"

# Construire le projet - chemin corrigé
RUN dotnet build "src/${SERVICE}/${SERVICE}.csproj" -c $CONFIGURATION -o /app/build

# Étape de publication
FROM build AS publish
ARG CONFIGURATION=Release
ARG SERVICE=DnDiscordAPI
RUN dotnet publish "src/${SERVICE}/${SERVICE}.csproj" -c $CONFIGURATION -o /app/publish /p:UseAppHost=false

# Étape finale : préparation de l'image pour l'exécution
FROM base AS final
ARG SERVICE=DnDiscordAPI
USER root
WORKDIR /app
COPY --from=publish /app/publish .
RUN ln -s "${SERVICE}.dll" docker_service_name.dll
ENTRYPOINT ["dotnet", "docker_service_name.dll"]