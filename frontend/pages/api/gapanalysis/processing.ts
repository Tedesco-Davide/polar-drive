import type { NextApiRequest, NextApiResponse } from 'next';

export const config = {
  api: {
    bodyParser: false,
    responseLimit: false,
    externalResolver: true,
  },
};

// Timeout breve per verificare lo stato di processing
const PROCESSING_TIMEOUT_MS = 30 * 1000; // 30 secondi

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  if (req.method !== 'GET') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
  const fullUrl = `${apiUrl}/api/pdfreports/gap-validation-processing`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), PROCESSING_TIMEOUT_MS);

    const response = await fetch(fullUrl, {
      method: 'GET',
      headers: {
        'Accept': 'application/json',
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
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    return res.json({ error: 'Empty response from backend' });

  } catch (error) {
    console.error(`[API Route] gap-validation-processing error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({
        error: 'Timeout',
        errorCode: 'TIMEOUT'
      });
    }

    return res.status(500).json({
      error: 'Errore interno',
      details: error instanceof Error ? error.message : String(error)
    });
  }
}
