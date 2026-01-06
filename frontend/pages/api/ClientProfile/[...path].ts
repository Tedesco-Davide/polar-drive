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

  const pathSegments = req.query.path as string[];
  const backendPath = pathSegments.join('/');
  const queryString = new URLSearchParams(
    Object.fromEntries(
      Object.entries(req.query)
        .filter(([key]) => key !== 'path')
        .map(([key, value]) => [key, String(value)])
    )
  ).toString();

  const fullUrl = `${apiUrl}/api/ClientProfile/${backendPath}${queryString ? `?${queryString}` : ''}`;

  try {
    // Long timeout for PDF generation (can take 30-60+ seconds)
    const timeoutMs = getUploadTimeoutMs();
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    if (req.method === 'POST') {
      const bodyBuffer = await buffer(req);

      const response = await fetch(fullUrl, {
        method: 'POST',
        body: bodyBuffer.length > 0 ? new Uint8Array(bodyBuffer) : undefined,
        headers: {
          'Content-Type': req.headers['content-type'] || 'application/json',
        },
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      const contentType = response.headers.get('content-type') || '';

      // Handle PDF response
      if (contentType.includes('application/pdf')) {
        const arrayBuffer = await response.arrayBuffer();
        const contentDisposition = response.headers.get('content-disposition');

        res.setHeader('Content-Type', 'application/pdf');
        if (contentDisposition) {
          res.setHeader('Content-Disposition', contentDisposition);
        }
        return res.status(response.status).send(Buffer.from(arrayBuffer));
      }

      // Handle JSON response
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
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      const contentType = response.headers.get('content-type') || '';

      // Handle PDF download
      if (contentType.includes('application/pdf')) {
        const arrayBuffer = await response.arrayBuffer();
        const contentDisposition = response.headers.get('content-disposition');

        res.setHeader('Content-Type', 'application/pdf');
        if (contentDisposition) {
          res.setHeader('Content-Disposition', contentDisposition);
        }
        return res.status(response.status).send(Buffer.from(arrayBuffer));
      }

      // Handle JSON response
      const responseText = await response.text();
      res.status(response.status);

      try {
        const json = JSON.parse(responseText);
        return res.json(json);
      } catch {
        return res.send(responseText);
      }
    }

    clearTimeout(timeoutId);
    return res.status(405).json({ error: 'Method not allowed' });

  } catch (error) {
    console.error(`[API Route] ClientProfile/${backendPath} error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({ error: 'Request timeout - PDF generation took too long' });
    }

    return res.status(500).json({
      error: 'Internal server error',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
