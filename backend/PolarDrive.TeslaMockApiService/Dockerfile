# backend/PolarDrive.TeslaMockApiService/Dockerfile

# Runtime (.NET 9)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 9090
ENV ASPNETCORE_URLS=http://+:9090

# 2) Build + publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo i .csproj per sfruttare la cache di restore
COPY backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj backend/PolarDrive.TeslaMockApiService/
RUN dotnet restore backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj

# Copia tutti i sorgenti backend e pubblica
COPY backend/ ./backend/
WORKDIR /src/backend/PolarDrive.TeslaMockApiService
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# Immagine finale runtime
FROM base AS final
WORKDIR /app

# Utente non-root
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/out .

# Health check servizio
# HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
#   CMD curl -f http://localhost:9090/health || exit 1

ENTRYPOINT ["dotnet", "PolarDrive.TeslaMockApiService.dll"]