# syntax=docker/dockerfile:1

# 1. Build the SPA
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

# 2. Publish the API with the SPA in wwwroot (one origin, one container)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/LmsTutor.Server/LmsTutor.Server.csproj src/LmsTutor.Server/
RUN dotnet restore src/LmsTutor.Server/LmsTutor.Server.csproj
COPY src/ src/
COPY --from=web /web/dist src/LmsTutor.Server/wwwroot/
RUN dotnet publish src/LmsTutor.Server/LmsTutor.Server.csproj -c Release -o /app --no-restore \
    && mkdir -p /data/keys

# 3. Runtime: chiseled image (no shell, no package manager), non-root user
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app ./
# The chiseled image cannot run mkdir, so the data folder is created in the build stage with the right owner.
COPY --from=build --chown=$APP_UID:$APP_UID /data /app/data

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/app/data/lmstutor.db" \
    DataProtection__KeysPath=/app/data/keys

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LmsTutor.Server.dll"]
