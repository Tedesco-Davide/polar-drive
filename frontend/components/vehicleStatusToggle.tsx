import { API_BASE_URL } from "@/utils/api";
import { useTranslation } from "next-i18next";
import axios from "axios";

type Props = {
  id: number;
  isActive: boolean;
  isFetching: boolean;
  field: "IsActive" | "IsFetching";
  onStatusChange: (newIsActive: boolean, newIsFetching: boolean) => void;
  setLoading: (value: boolean) => void;
  refreshWorkflowData: () => Promise<void>;
};

export default function VehicleStatusToggle({
  id,
  isActive,
  isFetching,
  field,
  onStatusChange,
  setLoading,
  refreshWorkflowData,
}: Props) {
  const { t } = useTranslation("");

  const toggleStatus = async () => {
    let newIsActive = isActive;
    let newIsFetching = isFetching;

    if (field === "IsActive") {
      if (isActive) {
        const confirm = window.confirm(
          t("admin.vehicleStatusToggle.confirmAction.toNotActive")
        );
        if (!confirm) return;
        newIsActive = false;
      } else {
        const confirm = window.confirm(
          t("admin.vehicleStatusToggle.confirmAction.backToActiveAndFetching")
        );
        if (!confirm) return;
        newIsActive = true;
        newIsFetching = true;
      }
    }

    if (field === "IsFetching") {
      if (isFetching && isActive) {
        alert(
          t("admin.vehicleStatusToggle.confirmAction.toNotFetchingFromActive")
        );
        return;
      }
      if (isFetching && !isActive) {
        const confirm = window.confirm(
          t(
            "admin.vehicleStatusToggle.confirmAction.toNotFetchingFromNotActive"
          )
        );
        if (!confirm) return;
        newIsFetching = false;
      }
      if (!isFetching && !isActive) {
        const confirm = window.confirm(
          t("admin.vehicleStatusToggle.confirmAction.toFetchingFromNotFetching")
        );
        if (!confirm) return;
        newIsFetching = true;
      }
    }

    try {
      setLoading(true);
      await axios.patch(`${API_BASE_URL}/api/ClientVehicles/${id}/status`, {
        isActive: newIsActive,
        isFetching: newIsFetching,
      });
      onStatusChange(newIsActive, newIsFetching);
      await refreshWorkflowData();
    } catch (err) {
      console.error(t("admin.vehicleStatusToggle.confirmAction.error"), err);
      alert(
        err instanceof Error
          ? err.message
          : t("admin.vehicleStatusToggle.confirmAction.error")
      );
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
      onClick={toggleStatus}
    >
      {label}
    </button>
  );
}
