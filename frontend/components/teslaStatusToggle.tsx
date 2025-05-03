import axios from "axios";
import { useState } from "react";
import { API_BASE_URL } from "@/utils/api";

type Props = {
  id: number;
  isActive: boolean;
  isFetching: boolean;
  field: "IsActive" | "IsFetching";
  onStatusChange: (newIsActive: boolean, newIsFetching: boolean) => void;
};

export default function TeslaStatusToggle({
  id,
  isActive,
  isFetching,
  field,
  onStatusChange,
}: Props) {
  const [loading, setLoading] = useState(false);

  const toggleStatus = async () => {
    const newIsActive = field === "IsActive" ? !isActive : isActive;
    const newIsFetching = field === "IsFetching" ? !isFetching : isFetching;

    if (newIsActive && !newIsFetching) {
      alert(
        "Un veicolo attivo deve anche essere in stato di acquisizione dati."
      );
      return;
    }

    try {
      setLoading(true);
      await axios.patch(
        `${API_BASE_URL}/api/ClientTeslaVehicles/${id}/status`,
        {
          isActive: newIsActive,
          isFetching: newIsFetching,
        }
      );
      onStatusChange(newIsActive, newIsFetching);
    } catch (error) {
      console.error("Errore aggiornamento stato Tesla:", error);
      alert("Errore nel salvataggio. Riprova.");
    } finally {
      setLoading(false);
    }
  };

  const label =
    field === "IsActive"
      ? isActive
        ? "✅ Active"
        : "🛑 NotActive"
      : isFetching
      ? "✅ Fetching"
      : "🛑 NotFetching";

  return (
    <button
      type="button"
      className={`active-fetching-checkbox`}
      disabled={loading}
      onClick={toggleStatus}
    >
      {label}
    </button>
  );
}
