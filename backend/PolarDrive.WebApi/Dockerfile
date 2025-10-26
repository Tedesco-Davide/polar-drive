# backend/PolarDrive.WebApi/Dockerfile

# Runtime (.NET 9)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
# Ascolta su
ENV ASPNETCORE_URLS=http://+:8080

# Build + publish
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

# Immagine finale runtime
FROM base AS final
WORKDIR /app

# ============================================
# INSTALLA NODE.JS + DIPENDENZE PER PDF
# ============================================
USER root

# Installa Node.js 20.x e dipendenze di sistema necessarie per Puppeteer
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libc6 \
    libcairo2 \
    libcups2 \
    libdbus-1-3 \
    libexpat1 \
    libfontconfig1 \
    libgbm1 \
    libgcc1 \
    libglib2.0-0 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    libstdc++6 \
    libx11-6 \
    libx11-xcb1 \
    libxcb1 \
    libxcomposite1 \
    libxcursor1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxi6 \
    libxrandr2 \
    libxrender1 \
    libxss1 \
    libxtst6 \
    lsb-release \
    xdg-utils \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# Verifica installazione Node.js
RUN node --version && npm --version

# Installa i pacchetti npm globali necessari per la conversione PDF
RUN npm install -g puppeteer@latest html-pdf-node@latest \
    && npm cache clean --force

# Crea directory per i report e assegna permessi
RUN mkdir -p /app/storage/reports && \
    mkdir -p /tmp && \
    chmod 777 /tmp

# ============================================
# UTENTE NON-ROOT
# ============================================
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app && \
    chown -R appuser:appuser /tmp

USER appuser

# ============================================
# COPIA APP E AVVIO
# ============================================
COPY --from=build --chown=appuser:appuser /app/out .

ENTRYPOINT ["dotnet", "PolarDrive.WebApi.dll"]