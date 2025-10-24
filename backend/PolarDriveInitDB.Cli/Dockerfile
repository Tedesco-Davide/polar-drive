# ---- Runtime (console .NET 9) ----------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

# Utente non-root
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# ---- Build + publish --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia i .csproj necessari (CLI + progetti referenziati) per cache restore
COPY backend/PolarDriveInitDB.Cli/PolarDriveInitDB.Cli.csproj backend/PolarDriveInitDB.Cli/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
RUN dotnet restore backend/PolarDriveInitDB.Cli/PolarDriveInitDB.Cli.csproj
COPY backend/ ./backend/

# Copia tutto il backend e pubblica
COPY backend/ ./backend/
WORKDIR /src/backend/PolarDriveInitDB.Cli
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# ---- Immagine finale --------------------------------------------------------
FROM base AS final
WORKDIR /app

# Copia l'output pubblicato
COPY --from=build /app/out ./
ENV DOTNET_ENVIRONMENT=Development
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "PolarDriveInitDB.Cli.dll"]
