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
  const timeoutMs = 60000; // 60 seconds

  if (!pathSegments || pathSegments.length === 0) {
    return res.status(400).json({ error: 'Invalid path' });
  }

  // Costruisci il path backend
  const backendPath = `/api/gapalerts/${pathSegments.join('/')}`;
  const fullUrl = `${apiUrl}${backendPath}`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    console.log(`[API Route] gapalerts/${pathSegments.join('/')} - ${req.method}`);

    // Prepara body per POST/PATCH
    let body: string | undefined;
    if (req.method === 'POST' || req.method === 'PATCH') {
      const bodyBuffer = await buffer(req);
      if (bodyBuffer.length > 0) {
        body = bodyBuffer.toString('utf-8');
      }
    }

    const response = await fetch(fullUrl, {
      method: req.method || 'GET',
      headers: {
        'Accept': 'application/json',
        ...(body && { 'Content-Type': 'application/json' }),
      },
      body,
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    const responseText = await response.text();
    res.status(response.status);

    if (responseText) {
      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        console.error(`[API Route] gapalerts/${pathSegments.join('/')} - invalid JSON:`, responseText.substring(0, 200));
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    return res.json({ error: 'Empty response from backend' });

  } catch (error) {
    console.error(`[API Route] gapalerts/${pathSegments.join('/')} error:`, error);

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
