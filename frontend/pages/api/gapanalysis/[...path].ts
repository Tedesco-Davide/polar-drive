import type { NextApiRequest, NextApiResponse } from 'next';
import { Readable } from 'stream';
import fs from 'fs';
import path from 'path';

export const config = {
  api: {
    bodyParser: false,
    responseLimit: false,
    externalResolver: true,
  },
};

async function buffer(readable: Readable): Promise<Buffer> {
  const chunks: Buffer[] = [];
  for await (const chunk of readable) {
    chunks.push(typeof chunk === 'string' ? Buffer.from(chunk) : chunk);
  }
  return Buffer.concat(chunks);
}

// Legge i timeout dal file di configurazione centralizzato
function getTimeouts(): { analysis: number; validate: number; status: number; download: number } {
  const defaults = { analysis: 15, validate: 5, status: 2, download: 5 };
  try {
    const configPath = path.join(process.cwd(), '..', 'app-config.json');
    const configContent = fs.readFileSync(configPath, 'utf-8');
    const appConfig = JSON.parse(configContent);
    const apiTimeouts = appConfig.gapAnalysis?.apiTimeouts;
    return {
      analysis: apiTimeouts?.analysisMinutes || defaults.analysis,
      validate: apiTimeouts?.validateMinutes || defaults.validate,
      status: apiTimeouts?.statusMinutes || defaults.status,
      download: apiTimeouts?.downloadMinutes || defaults.download,
    };
  } catch {
    console.warn('[API Route] Could not read app-config.json, using default timeouts');
    return defaults;
  }
}

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
  const pathSegments = req.query.path as string[];

  // Validazione path: deve avere almeno 2 segmenti [id, action]
  if (!pathSegments || pathSegments.length < 2) {
    return res.status(400).json({ error: 'Invalid path. Expected: /api/gapanalysis/{id}/{action}' });
  }

  const [id, action] = pathSegments;
  const timeouts = getTimeouts();

  // Mappa azione → configurazione
  type ActionConfig = {
    method: string;
    backendPath: string;
    timeoutMinutes: number;
    isPdfDownload?: boolean;
  };

  const actionMap: Record<string, ActionConfig> = {
    analysis: {
      method: 'GET',
      backendPath: `/api/pdfreports/${id}/gap-analysis`,
      timeoutMinutes: timeouts.analysis,
    },
    validate: {
      method: 'POST',
      backendPath: `/api/pdfreports/${id}/validate-gaps`,
      timeoutMinutes: timeouts.validate,
    },
    escalate: {
      method: 'POST',
      backendPath: `/api/gapalerts/${id}/escalate`,
      timeoutMinutes: timeouts.validate,
    },
    breach: {
      method: 'POST',
      backendPath: `/api/gapalerts/${id}/breach`,
      timeoutMinutes: timeouts.validate,
    },
    status: {
      method: 'GET',
      backendPath: `/api/pdfreports/${id}/gap-status`,
      timeoutMinutes: timeouts.status,
    },
    download: {
      method: 'GET',
      backendPath: `/api/pdfreports/${id}/download-gap-validation`,
      timeoutMinutes: timeouts.download,
      isPdfDownload: true,
    },
  };

  const config = actionMap[action];
  if (!config) {
    return res.status(400).json({
      error: `Invalid action: ${action}. Valid actions: ${Object.keys(actionMap).join(', ')}`
    });
  }

  // Verifica metodo HTTP
  if (req.method !== config.method) {
    return res.status(405).json({ error: `Method not allowed. Expected: ${config.method}` });
  }

  const fullUrl = `${apiUrl}${config.backendPath}`;
  const timeoutMs = config.timeoutMinutes * 60 * 1000;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    console.log(`[API Route] gapanalysis/${id}/${action} - timeout: ${config.timeoutMinutes}min`);

    // Prepara body per POST
    let body: string | undefined;
    if (req.method === 'POST') {
      const bodyBuffer = await buffer(req);
      if (bodyBuffer.length > 0) {
        body = bodyBuffer.toString('utf-8');
      }
    }

    const response = await fetch(fullUrl, {
      method: config.method,
      headers: {
        'Accept': config.isPdfDownload ? '*/*' : 'application/json',
        ...(req.method === 'POST' && { 'Content-Type': 'application/json' }),
      },
      body,
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    // Gestione download PDF
    if (config.isPdfDownload) {
      const contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/pdf')) {
        const pdfBuffer = await response.arrayBuffer();
        const contentDisposition = response.headers.get('content-disposition');

        res.setHeader('Content-Type', 'application/pdf');
        if (contentDisposition) {
          res.setHeader('Content-Disposition', contentDisposition);
        }
        return res.status(response.status).send(Buffer.from(pdfBuffer));
      }
    }

    // Gestione risposta JSON
    const responseText = await response.text();
    res.status(response.status);

    if (responseText) {
      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        console.error(`[API Route] gapanalysis/${id}/${action} - invalid JSON:`, responseText.substring(0, 200));
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    return res.json({ error: 'Empty response from backend' });

  } catch (error) {
    console.error(`[API Route] gapanalysis/${id}/${action} error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({
        error: `Timeout durante l'operazione (${config.timeoutMinutes} minuti)`,
        errorCode: 'TIMEOUT'
      });
    }

    return res.status(500).json({
      error: 'Errore interno',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
