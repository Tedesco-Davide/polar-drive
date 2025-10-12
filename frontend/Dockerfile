# frontend/Dockerfile

# Base Node
FROM node:20-alpine AS base

# 1) Dependencies (cache-friendly)
FROM base AS deps
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci --no-audit --no-fund

# 2) Build
FROM base AS builder
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY frontend/ ./
# opzionale: fissa una variabile pubblica a build-time
ARG NEXT_PUBLIC_API_BASE=""
ENV NEXT_PUBLIC_API_BASE=$NEXT_PUBLIC_API_BASE
RUN npm run build

# 3) Runtime (Next standalone)
FROM base AS runner
WORKDIR /app
ENV NODE_ENV=production
# copia artefatti standalone
COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
EXPOSE 3000
ENV PORT=3000
# non stiamo "run-nando" ora, ma questo è l'entrypoint corretto
CMD ["node", "server.js"]
