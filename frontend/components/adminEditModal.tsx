import { useEffect, ReactNode } from "react";

type Props = {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
};

export default function EditModal({ isOpen, title, children }: Props) {
  // 🔒 Blocca lo scroll della pagina
  useEffect(() => {
    if (isOpen) {
      document.body.classList.add("overflow-hidden");
    } else {
      document.body.classList.remove("overflow-hidden");
    }

    // Pulizia
    return () => {
      document.body.classList.remove("overflow-hidden");
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
