# backend/PolarDrive.WebApi/Dockerfile

# 1) Runtime (.NET 9) – solo esecuzione
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
# Ascolta su
ENV ASPNETCORE_URLS=http://+:8080

# 2) Build + publish (.NET 9 SDK)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia solo i .csproj per sfruttare la cache di restore
COPY backend/PolarDrive.WebApi/PolarDrive.WebApi.csproj backend/PolarDrive.WebApi/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
RUN dotnet restore backend/PolarDrive.WebApi/PolarDrive.WebApi.csproj

# Copia tutti i sorgenti backend e pubblica
COPY backend/ ./backend/
WORKDIR /src/backend/PolarDrive.WebApi
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# 3️⃣ Immagine finale (runtime)
FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "PolarDrive.WebApi.dll"]
