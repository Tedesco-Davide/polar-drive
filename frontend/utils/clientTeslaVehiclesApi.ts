export async function updateTeslaVehicleStatus(
  id: number,
  isActive: boolean,
  isFetching: boolean
): Promise<void> {
  const response = await fetch(`/api/ClientTeslaVehicles/${id}/status`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ isActive, isFetching }),
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(`Errore aggiornamento stato: ${error}`);
  }
}
