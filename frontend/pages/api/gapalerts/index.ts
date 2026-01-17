import type { NextApiRequest, NextApiResponse } from 'next';

export const config = {
  api: {
    bodyParser: true,
    responseLimit: false,
    externalResolver: true,
  },
};

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
  const timeoutMs = 30000; // 30 seconds

  // Costruisci URL con query params
  const queryString = new URLSearchParams(req.query as Record<string, string>).toString();
  const fullUrl = `${apiUrl}/api/gapalerts${queryString ? `?${queryString}` : ''}`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    console.log(`[API Route] gapalerts - ${req.method} ${fullUrl}`);

    const response = await fetch(fullUrl, {
      method: req.method,
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json',
      },
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
        console.error(`[API Route] gapalerts - invalid JSON:`, responseText.substring(0, 200));
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    return res.json({ error: 'Empty response from backend' });

  } catch (error) {
    console.error(`[API Route] gapalerts error:`, error);

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
