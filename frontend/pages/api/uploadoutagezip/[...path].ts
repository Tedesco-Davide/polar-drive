import type { NextApiRequest, NextApiResponse } from 'next';
import { Readable } from 'stream';
import { getUploadTimeoutMs } from '@/utils/appConfig';

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

  // Build the backend URL path from the catch-all path segments
  const pathSegments = req.query.path as string[];
  const backendPath = pathSegments.join('/');
  const queryString = new URLSearchParams(
    Object.fromEntries(
      Object.entries(req.query)
        .filter(([key]) => key !== 'path')
        .map(([key, value]) => [key, String(value)])
    )
  ).toString();

  const fullUrl = `${apiUrl}/api/UploadOutageZip/${backendPath}${queryString ? `?${queryString}` : ''}`;

  try {
    // Handle POST requests (upload ZIP to existing outage)
    if (req.method === 'POST') {
      const bodyBuffer = await buffer(req);
      const timeoutMs = getUploadTimeoutMs();

      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

      const response = await fetch(fullUrl, {
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
      res.status(response.status);

      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        return res.send(responseText);
      }
    }

    // Handle GET requests
    if (req.method === 'GET') {
      const response = await fetch(fullUrl, {
        method: 'GET',
        headers: {
          'Accept': '*/*',
        },
      });

      const contentType = response.headers.get('content-type') || '';

      // Handle ZIP file downloads
      if (contentType.includes('application/zip')) {
        const arrayBuffer = await response.arrayBuffer();
        const contentDisposition = response.headers.get('content-disposition');

        res.setHeader('Content-Type', 'application/zip');
        if (contentDisposition) {
          res.setHeader('Content-Disposition', contentDisposition);
        }
        return res.status(response.status).send(Buffer.from(arrayBuffer));
      }

      // Handle JSON responses
      const responseText = await response.text();
      res.status(response.status);

      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        return res.send(responseText);
      }
    }

    return res.status(405).json({ error: 'Method not allowed' });

  } catch (error) {
    console.error(`[API Route] UploadOutageZip/${backendPath} error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({ error: 'Request timeout' });
    }

    return res.status(500).json({
      error: 'Internal server error',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
