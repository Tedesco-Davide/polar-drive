export async function logFrontendEvent(
  source: string,
  level: "INFO" | "WARNING" | "ERROR" | "DEBUG",
  message: string,
  details?: string
) {
  try {
    // ✅ Usa URL relativo (il proxy Next.js lo gestisce)
    await fetch('/api/Logs', {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        source,
        level,
        message,
        details,
      }),
    });
  } catch (logErr) {
    console.error("⚠️ Failed to log frontend event:", logErr);
  }
}