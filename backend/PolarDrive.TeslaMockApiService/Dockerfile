# backend/PolarDriveInitDB.Cli/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/PolarDriveInitDB.Cli/PolarDriveInitDB.Cli.csproj backend/PolarDriveInitDB.Cli/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
RUN dotnet restore backend/PolarDriveInitDB.Cli/PolarDriveInitDB.Cli.csproj

COPY backend/ ./backend/
WORKDIR /src/backend/PolarDriveInitDB.Cli
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
# utente non-root come negli altri
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser
COPY --from=build /app/out ./
ENV DOTNET_ENVIRONMENT=Development
ENTRYPOINT ["dotnet","PolarDriveInitDB.Cli.dll"]
