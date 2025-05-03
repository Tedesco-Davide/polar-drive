"use client";

import { useEffect, useState } from "react";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import classNames from "classnames";
import Chip from "./chip";

export default function Hero() {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <section className="relative w-full overflow-hidden pb-24 pt-36">
      {/* Griglia di sfondo */}
      {mounted && (
        <div
          className={classNames(
            "absolute inset-0 z-0 bg-background bg-[length:40px_40px]",
            {
              "bg-hero-grid-light": mounted && theme === "light",
              "dark:bg-hero-grid": mounted && theme === "dark",
            }
          )}
        />
      )}

      {/* Alone centrale */}
      <div className="absolute inset-0 z-10 pointer-events-none">
        <div className="absolute left-1/2 top-32 -translate-x-1/2 w-[600px] h-[600px] rounded-full bg-[#5c4de14a] dark:bg-[#5c4de130] blur-3xl opacity-60"></div>
      </div>

      {/* Contenuto hero */}
      <div className="relative z-20 max-w-4xl mx-auto text-center px-6">
        <h1 className="text-2xl md:text-4xl font-bold text-polarNight dark:text-articWhite">
          {t("hero.title")}
        </h1>
        <p className="mt-6 text-lg text-polarNight dark:text-articWhite/80 max-w-xl mx-auto">
          <Chip>{t("hero.subtitle")}</Chip>
        </p>
        <button className="mt-8 w-full sm:w-auto px-6 py-3 rounded-full bg-coldIndigo text-articWhite font-medium shadow-md hover:bg-indigo-600 transition">
          {t("hero.CTA")}
        </button>
      </div>
    </section>
  );
}
