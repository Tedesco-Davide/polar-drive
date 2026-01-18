import { useRef } from "react";
import classNames from "classnames";

interface GenericChipProps {
  children: React.ReactNode;
  onClick?: () => void;
  className?: string;
}

export default function AdminGenericChip({ children, onClick, className }: GenericChipProps) {
  const chipRef = useRef<HTMLButtonElement>(null);

  const handleClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    const button = chipRef.current;
    if (!button) return;

    // Crea lo span ripple
    const circle = document.createElement("span");
    const diameter = Math.max(button.clientWidth, button.clientHeight);
    const radius = diameter / 2;

    circle.style.width = circle.style.height = `${diameter}px`;
    circle.style.left = `${
      e.clientX - button.getBoundingClientRect().left - radius
    }px`;
    circle.style.top = `${
      e.clientY - button.getBoundingClientRect().top - radius
    }px`;
    circle.className = "ripple";

    // Rimuovi eventuali ripple precedenti
    const ripple = button.getElementsByClassName("ripple")[0];
    if (ripple) ripple.remove();

    button.appendChild(circle);

    if (onClick) onClick();
  };

  return (
    <button
      ref={chipRef}
      onClick={handleClick}
      className={classNames(
        "relative overflow-hidden rounded-md text-sm border transition-all select-text cursor-default",
        "hover:shadow-md focus:outline-none",
        "py-1 px-2 backdrop-blur",
        className
      )}
    >
      {children}
    </button>
  );
}
