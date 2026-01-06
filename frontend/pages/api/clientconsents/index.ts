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

  // Handle GET requests (list, resolve-ids, download, etc.)
  if (req.method === 'GET') {
    try {
      const queryString = new URLSearchParams(req.query as Record<string, string>).toString();
      const url = `${apiUrl}/api/ClientConsents${queryString ? `?${queryString}` : ''}`;

      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
        },
      });

      const contentType = response.headers.get('content-type') || '';

      if (contentType.includes('application/zip')) {
        const buffer = await response.arrayBuffer();
        res.setHeader('Content-Type', 'application/zip');
        res.setHeader('Content-Disposition', response.headers.get('content-disposition') || 'attachment');
        return res.status(response.status).send(Buffer.from(buffer));
      }

      const responseText = await response.text();
      res.status(response.status);

      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        return res.send(responseText);
      }
    } catch (error) {
      console.error('[API Route] ClientConsents GET error:', error);
      return res.status(500).json({
        error: 'Internal server error',
        details: error instanceof Error ? error.message : String(error)
      });
    }
  }

  // Handle POST requests (create consent with ZIP upload)
  if (req.method === 'POST') {
    try {
      const bodyBuffer = await buffer(req);
      const contentType = req.headers['content-type'];
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 120000); // 2 minuti

      const response = await fetch(`${apiUrl}/api/ClientConsents`, {
        method: 'POST',
        body: bodyBuffer as unknown as BodyInit,
        headers: {
          'Content-Type': contentType || 'multipart/form-data',
          'Content-Length': bodyBuffer.length.toString(),
        },
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      const responseText = await response.text();
      res.status(response.status);

      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        return res.send(responseText);
      }
    } catch (error) {
      console.error('[API Route] ClientConsents POST error:', error);

      if (error instanceof Error && error.name === 'AbortError') {
        return res.status(504).json({ error: 'Request timeout' });
      }

      return res.status(500).json({
        error: 'Internal server error',
        details: error instanceof Error ? error.message : String(error)
      });
    }
  }

  return res.status(405).json({ error: 'Method not allowed' });
}
