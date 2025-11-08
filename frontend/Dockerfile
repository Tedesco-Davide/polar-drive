# frontend/Dockerfile

FROM node:20-alpine AS base

# Dependencies (cache-friendly)
FROM base AS deps
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci --no-audit --no-fund

# Build
FROM base AS builder
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules

# Copia Frontend (include i fonts)
COPY frontend/ ./

RUN npm run build

# Runtime (Next standalone)
FROM base AS runner
WORKDIR /app
ENV NODE_ENV=production

# ✅ INSTALLA FONT SYSTEM PER PUPPETEER
RUN apk add --no-cache \
    font-noto-emoji \
    font-noto \
    fontconfig \
    && fc-cache -f

# Utente non-root
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

# ✅ COPIA FONT NEL CONTAINER FINALE
COPY --from=builder --chown=nextjs:nodejs /app/public/fonts ./public/fonts
COPY --from=builder --chown=nextjs:nodejs /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000

CMD ["node", "server.js"]