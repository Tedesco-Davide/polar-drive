import { useEffect, ReactNode } from "react";
import { logFrontendEvent } from "@/utils/logger";

type ModalEditTableRowsProps = {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
};

export default function ModalEditTableRows({ isOpen, title, children }: ModalEditTableRowsProps) {
  useEffect(() => {
    try {
      if (isOpen) {
        document.body.classList.add("overflow-hidden");
        logFrontendEvent(
          "ModalEditTableRows",
          "INFO",
          "Modal opened and body scroll disabled"
        );
      } else {
        document.body.classList.remove("overflow-hidden");
        logFrontendEvent(
          "ModalEditTableRows",
          "INFO",
          "Modal closed and body scroll restored"
        );
      }
    } catch (err) {
      const errDetails = err instanceof Error ? err.message : String(err);
      logFrontendEvent(
        "ModalEditTableRows",
        "ERROR",
        "Error occurred while toggling modal scroll behavior",
        errDetails
      );
    }

    return () => {
      try {
        document.body.classList.remove("overflow-hidden");
        logFrontendEvent(
          "ModalEditTableRows",
          "INFO",
          "Modal cleanup triggered and body scroll restored"
        );
      } catch (cleanupErr) {
        const errDetails =
          cleanupErr instanceof Error ? cleanupErr.message : String(cleanupErr);
        logFrontendEvent(
          "ModalEditTableRows",
          "ERROR",
          "Error occurred during modal cleanup",
          errDetails
        );
      }
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="fixed top-[64px] inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur">
      <div
        className="
          w-full h-full p-6 relative overflow-y-auto
          bg-softWhite dark:bg-polarNight rounded-none shadow-none
          md:rounded-lg md:shadow-lg md:h-auto md:w-11/12
        "
      >
        <h2 className="text-xl font-semibold text-polarNight dark:text-softWhite mb-4">
          {title}
        </h2>
        <div>{children}</div>
      </div>
    </div>
  );
}
