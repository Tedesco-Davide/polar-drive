import fs from 'fs';

interface AppConfigLimits {
  maxUploadSizeBytes: number;
  httpLongRequestTimeoutMinutes: number;
}

interface AppConfig {
  limits: AppConfigLimits;
}

let cachedConfig: AppConfig | null = null;
let lastModified: number = 0;

/**
 * Legge la configurazione da app-config.json con caching e hot-reload.
 * Il file viene riletto automaticamente se modificato.
 * Funziona solo server-side (API routes).
 */
export function getAppConfig(): AppConfig {
  const configPath = '/app/config/app-config.json';

  try {
    const stats = fs.statSync(configPath);

    // Rileggi se il file e' stato modificato
    if (!cachedConfig || stats.mtimeMs > lastModified) {
      const content = fs.readFileSync(configPath, 'utf-8');
      cachedConfig = JSON.parse(content);
      lastModified = stats.mtimeMs;
    }

    return cachedConfig!;
  } catch {
    // Fallback se file non trovato (sviluppo locale senza Docker)
    return {
      limits: {
        maxUploadSizeBytes: 100000000,
        httpLongRequestTimeoutMinutes: 15
      }
    };
  }
}

/**
 * Restituisce il timeout per upload in millisecondi.
 * Legge da app-config.json: limits.httpLongRequestTimeoutMinutes
 */
export function getUploadTimeoutMs(): number {
  return getAppConfig().limits.httpLongRequestTimeoutMinutes * 60 * 1000;
}

/**
 * Restituisce la dimensione massima per upload in bytes.
 * Legge da app-config.json: limits.maxUploadSizeBytes
 */
export function getMaxUploadSizeBytes(): number {
  return getAppConfig().limits.maxUploadSizeBytes;
}
