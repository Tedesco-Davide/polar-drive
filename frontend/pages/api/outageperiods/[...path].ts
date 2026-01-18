import type { NextApiRequest, NextApiResponse } from 'next';
import { Readable } from 'stream';

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

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
  const pathSegments = req.query.path as string[];

  if (!pathSegments || pathSegments.length < 2) {
    return res.status(400).json({
      error: 'Invalid path. Expected: /api/outageperiods/{id}/{action}'
    });
  }

  const [id, action] = pathSegments;
  const timeoutMs = 60000;

  type ActionConfig = {
    method: string;
    backendPath: string;
    isZipDownload?: boolean;
  };

  const actionMap: Record<string, ActionConfig> = {
    'download-zip': {
      method: 'GET',
      backendPath: `/api/OutagePeriods/${id}/download-zip`,
      isZipDownload: true,
    },
    'resolve': {
      method: 'PATCH',
      backendPath: `/api/OutagePeriods/${id}/resolve`,
    },
    'notes': {
      method: 'PATCH',
      backendPath: `/api/OutagePeriods/${id}/notes`,
    },
  };

  const config = actionMap[action];
  if (!config) {
    return res.status(400).json({
      error: `Invalid action: ${action}. Valid actions: ${Object.keys(actionMap).join(', ')}`
    });
  }

  if (req.method !== config.method) {
    return res.status(405).json({
      error: `Method not allowed. Expected: ${config.method}`
    });
  }

  const fullUrl = `${apiUrl}${config.backendPath}`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    console.log(`[API Route] outageperiods/${id}/${action} - ${config.method}`);

    let body: string | undefined;
    if (req.method === 'PATCH') {
      const bodyBuffer = await buffer(req);
      if (bodyBuffer.length > 0) {
        body = bodyBuffer.toString('utf-8');
      }
    }

    const response = await fetch(fullUrl, {
      method: config.method,
      headers: {
        'Accept': config.isZipDownload ? '*/*' : 'application/json',
        ...(body && { 'Content-Type': 'application/json' }),
      },
      body,
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    if (config.isZipDownload) {
      const contentType = response.headers.get('content-type') || '';

      if (contentType.includes('application/json')) {
        const jsonResponse = await response.text();
        res.status(response.status);
        try {
          return res.json(JSON.parse(jsonResponse));
        } catch {
          return res.send(jsonResponse);
        }
      }

      if (contentType.includes('application/zip') || contentType.includes('application/octet-stream')) {
        const arrayBuffer = await response.arrayBuffer();
        const contentDisposition = response.headers.get('content-disposition');

        res.setHeader('Content-Type', 'application/zip');
        if (contentDisposition) {
          res.setHeader('Content-Disposition', contentDisposition);
        }
        return res.status(response.status).send(Buffer.from(arrayBuffer));
      }
    }

    const responseText = await response.text();
    res.status(response.status);

    if (responseText) {
      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        console.error(`[API Route] outageperiods/${id}/${action} - invalid JSON:`, responseText.substring(0, 200));
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    return res.json({ success: true });

  } catch (error) {
    console.error(`[API Route] outageperiods/${id}/${action} error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({
        error: 'Timeout during operation',
        errorCode: 'TIMEOUT'
      });
    }

    return res.status(500).json({
      error: 'Internal error',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
