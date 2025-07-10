"use client";
import { useRouter } from "next/router";
import Image from "next/image";
import { logFrontendEvent } from "@/utils/logger";
import { useState } from "react";

export default function LanguageSwitcher() {
  const router = useRouter();
  const [isChanging, setIsChanging] = useState(false);

  const switchTo = (lng: string) => {
    // Evita doppi click durante il cambio
    if (isChanging || router.locale === lng) return;

    setIsChanging(true);

    logFrontendEvent(
      "LanguageSwitcher",
      "INFO",
      `Language switched to ${lng}`,
      `Current path: ${router.asPath}`
    );

    // ✅ Preserva la posizione di scroll
    const scrollPosition = window.scrollY;

    // ✅ Usa replace invece di push per evitare problemi di navigazione
    router
      .push(router.pathname, router.asPath, { locale: lng, shallow: false })
      .then(() => {
        // ✅ Ripristina la posizione di scroll dopo il cambio lingua
        setTimeout(() => {
          window.scrollTo(0, scrollPosition);
          setIsChanging(false);
        }, 100);
      })
      .catch(() => {
        setIsChanging(false);
      });
  };

  return (
    <div className="hidden md:flex xl:ml-[69px] items-center space-x-4">
      <button
        onClick={() => switchTo("it")}
        aria-label="Italiano"
        disabled={isChanging}
        className={`transition-opacity duration-200 ${
          isChanging ? "opacity-50 cursor-not-allowed" : "hover:opacity-80"
        } ${router.locale === "it" ? "ring-2 ring-blue-500 rounded" : ""}`}
      >
        <Image src="/icons/flag-it.svg" alt="IT" width={24} height={16} />
      </button>
      <button
        onClick={() => switchTo("en")}
        aria-label="English"
        disabled={isChanging}
        className={`transition-opacity duration-200 ${
          isChanging ? "opacity-50 cursor-not-allowed" : "hover:opacity-80"
        } ${router.locale === "en" ? "ring-2 ring-blue-500 rounded" : ""}`}
      >
        <Image src="/icons/flag-en.svg" alt="EN" width={24} height={16} />
      </button>
    </div>
  );
}
