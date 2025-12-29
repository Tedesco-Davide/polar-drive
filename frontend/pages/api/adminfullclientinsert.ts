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
  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';

  try {
    // Leggi il body raw come buffer
    const bodyBuffer = await buffer(req);

    // Inoltra direttamente al backend
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 120000); // 2 minuti

    const response = await fetch(`${apiUrl}/api/AdminFullClientInsert`, {
      method: 'POST',
      body: new Uint8Array(bodyBuffer),
      headers: {
        'Content-Type': req.headers['content-type'] || 'multipart/form-data',
        'Content-Length': bodyBuffer.length.toString(),
      },
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    const responseText = await response.text();

    // Imposta gli header della risposta
    res.status(response.status);

    // Prova a parsare come JSON
    try {
      const json = JSON.parse(responseText);
      return res.json(json);
    } catch {
      return res.send(responseText);
    }
  } catch (error) {
    console.error('[API Route] AdminFullClientInsert error:', error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({ error: 'Request timeout' });
    }

    return res.status(500).json({
      error: 'Internal server error',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
