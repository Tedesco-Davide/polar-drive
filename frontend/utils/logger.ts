import { API_BASE_URL } from "@/utils/api";

export async function logFrontendEvent(
  source: string,
  level: "INFO" | "WARNING" | "ERROR" | "DEBUG",
  message: string,
  details?: string
) {
  try {
    await fetch(`${API_BASE_URL}/api/Logs`, {
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
