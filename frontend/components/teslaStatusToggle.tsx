import { useState } from "react";
import { API_BASE_URL } from "@/utils/api";
import { useTranslation } from "next-i18next";
import axios from "axios";

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
  const { t } = useTranslation("");

  const toggleStatus = async () => {
    let newIsActive = isActive;
    let newIsFetching = isFetching;

    if (field === "IsActive") {
      if (isActive) {
        const confirm = window.confirm(
          t("admin.teslaStatusToggle.confirmAction.toNotActive")
        );
        if (!confirm) return;
        newIsActive = false;
      } else {
        const confirm = window.confirm(
          t("admin.teslaStatusToggle.confirmAction.backToActiveAndFetching")
        );
        if (!confirm) return;
        newIsActive = true;
        newIsFetching = true;
      }
    }

    if (field === "IsFetching") {
      if (isFetching && isActive) {
        alert(
          t("admin.teslaStatusToggle.confirmAction.toNotFetchingFromActive")
        );
        return;
      }
      if (isFetching && !isActive) {
        const confirm = window.confirm(
          t("admin.teslaStatusToggle.confirmAction.toNotFetchingFromNotActive")
        );
        if (!confirm) return;
        newIsFetching = false;
      }
      if (!isFetching && !isActive) {
        const confirm = window.confirm(
          t("admin.teslaStatusToggle.confirmAction.toFetchingFromNotFetching")
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
      console.error(t("admin.teslaStatusToggle.confirmAction.error"), error);
      alert(t("admin.teslaStatusToggle.confirmAction.error") + "\n" + error);
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
