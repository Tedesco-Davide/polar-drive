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
    let newIsActive = isActive;
    let newIsFetching = isFetching;

    if (field === "IsActive") {
      if (isActive) {
        const confirm = window.confirm(
          "Sei sicuro di voler passare questa tesla in stato NotActive? Premi conferma per confermare."
        );
        if (!confirm) return;
        newIsActive = false;
      } else {
        const confirm = window.confirm(
          "Stato tesla verificato a NotActive e NotFetching. Sei sicuro di voler riattivare questa tesla? Questo comporterà il ripristino dello stato ad Active, nonché il ripristino dell'acquisizione dati con cambio stato a Fetching. Premi conferma per confermare."
        );
        if (!confirm) return;
        newIsActive = true;
        newIsFetching = true;
      }
    }

    if (field === "IsFetching") {
      if (isFetching && isActive) {
        alert(
          "Non è possibile disattivare l'acquisizione Dati, per una tesla in stato Active."
        );
        return;
      }
      if (isFetching && !isActive) {
        const confirm = window.confirm(
          "Stato tesla verificato a NotActive. Sei sicuro di voler disattivare l'acquisizione Dati per questa tesla e portare lo stato a NotFetching? Premi conferma per confermare."
        );
        if (!confirm) return;
        newIsFetching = false;
      }
      if (!isFetching && !isActive) {
        const confirm = window.confirm(
          "Acquisizione Dati per questa tesla verificato a NotFetching. Sei sicuro di voler riattivare l'acquisizione Dati e riportare lo stato a Fetching? Premi conferma per confermare."
        );
        if (!confirm) return;
        newIsFetching = true;
      }
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
      ? "📡 Fetching"
      : "⛔ NotFetching";

  return (
    <button
      type="button"
      className="active-fetching-checkbox"
      disabled={loading}
      onClick={toggleStatus}
    >
      {label}
    </button>
  );
}
