import type { NextApiRequest, NextApiResponse } from 'next';

export const config = {
  api: {
    bodyParser: true,
    responseLimit: false,
    externalResolver: true,
  },
};

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  if (req.method !== 'GET') {
    return res.status(405).json({ error: 'Method not allowed' });
  }

  const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
  const timeoutMs = 10000; // 10 seconds
  const fullUrl = `${apiUrl}/api/gapalerts/monitoring-interval`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

    console.log(`[API Route] gapalerts/monitoring-interval - GET ${fullUrl}`);

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
        console.error(`[API Route] gapalerts/monitoring-interval - invalid JSON:`, responseText.substring(0, 200));
        return res.status(500).json({
          error: 'Invalid response from backend',
          details: responseText.substring(0, 500)
        });
      }
    }

    // Default fallback
    return res.json({ checkIntervalMinutes: 60 });

  } catch (error) {
    console.error(`[API Route] gapalerts/monitoring-interval error:`, error);

    if (error instanceof Error && error.name === 'AbortError') {
      return res.status(504).json({
        error: 'Timeout during operation',
        errorCode: 'TIMEOUT'
      });
    }

    // Fallback to default value instead of error
    return res.json({ checkIntervalMinutes: 60 });
  }
}
