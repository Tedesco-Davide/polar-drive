# backend/PolarDrive.TeslaMockApiService/Dockerfile
# --- BUILD ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo i .csproj per sfruttare la cache dei layer
COPY backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj backend/PolarDrive.TeslaMockApiService/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
# Se il servizio referenzia altri progetti, aggiungili qui come nei due esempi sopra
# COPY backend/PolarDrive.Domain/PolarDrive.Domain.csproj backend/PolarDrive.Domain/

RUN dotnet restore backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj

# Ora porta dentro tutto il backend
COPY backend/ ./backend/

WORKDIR /src/backend/PolarDrive.TeslaMockApiService
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# --- RUNTIME ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Utente non-root (coerente con gli altri container)
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Env di sviluppo + bind esplicito su 0.0.0.0:9090
ENV DOTNET_ENVIRONMENT=Development \
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://0.0.0.0:9090

# Porta interna del servizio
EXPOSE 9090

COPY --from=build /app/out ./
ENTRYPOINT ["dotnet","PolarDrive.TeslaMockApiService.dll"]
