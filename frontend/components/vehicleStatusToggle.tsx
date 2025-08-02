import { API_BASE_URL } from "@/utils/api";
import { useTranslation } from "next-i18next";
import { logFrontendEvent } from "@/utils/logger";
import { CircleCheck, CircleX } from "lucide-react";
import axios from "axios";

type Props = {
  id: number;
  isActive: boolean;
  isFetching: boolean;
  field: "IsActive" | "IsFetching";
  disabled?: boolean;
  onStatusChange: (newIsActive: boolean, newIsFetching: boolean) => void;
  setLoading: (value: boolean) => void;
  refreshWorkflowData: () => Promise<void>;
};

export default function VehicleStatusToggle({
  id,
  isActive,
  isFetching,
  field,
  disabled,
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
        logFrontendEvent(
          "VehicleStatusToggle",
          "INFO",
          "User cancelled toggle operation",
          `Field: ${field}, From: isActive=${isActive}, isFetching=${isFetching}`
        );
        newIsActive = false;
      } else {
        if (isFetching) {
          const confirm = window.confirm(
            t("admin.vehicleStatusToggle.confirmAction.backToActiveOnly")
          );
          if (!confirm) return;
          newIsActive = true;
        }
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
        IsActive: newIsActive,
        IsFetching: newIsFetching,
      });
      onStatusChange(newIsActive, newIsFetching);
      logFrontendEvent(
        "VehicleStatusToggle",
        "INFO",
        "Vehicle status successfully updated",
        `Vehicle ID: ${id}, isActive: ${newIsActive}, isFetching: ${newIsFetching}`
      );
      await refreshWorkflowData();
    } catch (err) {
      console.error(t("admin.vehicleStatusToggle.confirmAction.error"), err);
      logFrontendEvent(
        "VehicleStatusToggle",
        "ERROR",
        "Failed to update vehicle status",
        err instanceof Error ? err.message : String(err)
      );
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
    field === "IsActive" ? (
      isActive ? (
        <div className="flex justify-center text-green-600 gap-1">
          <CircleCheck size={20} />
          <span className="text-polarNight dark:text-articWhite">Active</span>
        </div>
      ) : (
        <div className="flex justify-center text-red-600 gap-1">
          <CircleX size={20} />
          <span className="text-polarNight dark:text-articWhite">
            NotActive
          </span>
        </div>
      )
    ) : isFetching ? (
      <div className="flex justify-center text-green-600 gap-1">
        <CircleCheck size={20} />
        <span className="text-polarNight dark:text-articWhite">Fetching</span>
      </div>
    ) : (
      <div className="flex justify-center text-red-600 gap-1">
        <CircleX size={20} />
        <span className="text-polarNight dark:text-articWhite">
          NotFetching
        </span>
      </div>
    );

  return (
    <button
      type="button"
      className="workflow-action"
      onClick={(e) => {
        if (disabled) {
          e.preventDefault();
          return;
        }
        toggleStatus();
      }}
      disabled={disabled}
    >
      {label}
    </button>
  );
}
