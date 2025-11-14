# backend/PolarDrive.TeslaMockApiService/Dockerfile

# --- BUILD ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj backend/PolarDrive.TeslaMockApiService/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
# Se il servizio referenzia altri progetti, aggiungili qui

RUN dotnet restore backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj

COPY backend/ ./backend/
WORKDIR /src/backend/PolarDrive.TeslaMockApiService
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# --- RUNTIME ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Preparazione cartelle e permessi
USER root
RUN mkdir -p /app/TempFiles \
    && mkdir -p /app/logs \
    && mkdir -p /app/Logs \
    && chmod -R 775 /app/TempFiles /app/logs \
    && chown -R app:app /app

# Torna all'utente non-root
USER app

EXPOSE 9090
ENV ASPNETCORE_URLS=http://+:9090

COPY --from=build --chown=app:app /app/out .

ENTRYPOINT ["dotnet", "PolarDrive.TeslaMockApiService.dll"]
