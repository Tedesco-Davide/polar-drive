# backend/PolarDrive.TeslaMockApiService/Dockerfile

# 1) Runtime (.NET 9) – solo esecuzione
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 9090
# Ascolta su tutte le interfacce, porta 9090 (HTTP)
ENV ASPNETCORE_URLS=http://+:9090

# 2) Build + publish (.NET 9 SDK)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo i .csproj per sfruttare la cache di restore
COPY backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj backend/PolarDrive.TeslaMockApiService/
RUN dotnet restore backend/PolarDrive.TeslaMockApiService/PolarDrive.TeslaMockApiService.csproj

# Copia tutti i sorgenti backend e pubblica
COPY backend/ ./backend/
WORKDIR /src/backend/PolarDrive.TeslaMockApiService
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# 3) Immagine finale runtime
FROM base AS final
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "PolarDrive.TeslaMockApiService.dll"]
