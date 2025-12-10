# backend/PolarDrive.WebApi/Dockerfile

# Runtime (.NET 10)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# FONT EMOJI + TIMEZONE
RUN apt-get update && apt-get install -y \
    tzdata \
    fonts-noto-color-emoji \
    fontconfig \
    && fc-cache -f \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Europe/Rome

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Build + publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/PolarDrive.WebApi/PolarDrive.WebApi.csproj backend/PolarDrive.WebApi/
COPY backend/PolarDrive.Data/PolarDrive.Data.csproj backend/PolarDrive.Data/
RUN dotnet restore backend/PolarDrive.WebApi/PolarDrive.WebApi.csproj

COPY backend/ ./backend/
WORKDIR /src/backend/PolarDrive.WebApi
RUN dotnet publish -c Release -o /app/out /p:UseAppHost=false

# Immagine finale runtime
FROM base AS final
WORKDIR /app

# ============================================
# INSTALLA NODE.JS + PUPPETEER
# ============================================
USER root

# Installa Node.js 20.x e dipendenze (incluso libasound2t64)
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libasound2t64 \
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

RUN echo "🔍 Verifying Node.js installation..." \
    && node --version \
    && npm --version

# package.json minimale + Puppeteer
WORKDIR /app
RUN echo '{"name": "polardrive-pdf", "version": "1.0.0", "description": "PDF generation for PolarDrive"}' > package.json

RUN echo "📦 Installing Puppeteer..." \
    && npm install puppeteer@^24.15.0 --save --omit=optional \
    && npm cache clean --force

RUN echo "✅ Verifying Puppeteer installation..." \
    && node -e "const puppeteer = require('puppeteer'); console.log('✅ Puppeteer installed successfully');"

# Directory che usi nel codice + font
RUN mkdir -p /app/storage/reports \
    && mkdir -p /app/TempFiles \
    && mkdir -p /app/logs/puppeteer \
    && mkdir -p /app/logs \
    && mkdir -p /app/Logs \
    && mkdir -p /tmp \
    && mkdir -p /usr/share/fonts/truetype/satoshi \
    && mkdir -p /app/wwwroot/fonts/satoshi

# Copia font e ricostruisci cache font
COPY frontend/public/fonts/satoshi/*.b64 /app/wwwroot/fonts/satoshi/
COPY frontend/public/fonts/satoshi/*.woff2 /usr/share/fonts/truetype/satoshi/
RUN fc-cache -fv

# Permessi per l'utente 'app'
RUN chmod 777 /tmp \
    && chmod -R 775 /app/storage /app/TempFiles /app/logs /app/Logs \
    && chown -R app:app /app/storage /app/TempFiles /app/logs /app/Logs /app/wwwroot

# Usa l'utente non-root
USER app

# Scarica Chrome per Puppeteer come utente 'app'
RUN echo "🌐 Downloading Chrome for Puppeteer as app..." \
    && cd /app \
    && npx puppeteer@^24.15.0 browsers install chrome \
    && echo "✅ Chrome downloaded successfully"

# Verifica Chrome installato usando la HOME corretta
RUN echo "✅ Verifying Chrome installation..." \
    && echo "HOME is: $HOME" \
    && ls -la "$HOME/.cache/puppeteer" \
    && ls -la "$HOME/.cache/puppeteer/chrome" \
    && echo "✅ Chrome verified"

# ============================================
# COPIA APP E AVVIO
# ============================================
COPY --from=build --chown=app:app /app/out .

ENV NODE_PATH=/app/node_modules

ENTRYPOINT ["dotnet", "PolarDrive.WebApi.dll"]
