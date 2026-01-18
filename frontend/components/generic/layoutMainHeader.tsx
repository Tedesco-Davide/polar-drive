"use client";

import { useState, useEffect, useRef } from "react";
import { useTheme } from "next-themes";
import { Sun, Moon } from "lucide-react";
import { useTranslation } from "next-i18next";
import { useRouter } from "next/router";
import { gsap } from "gsap";
import Link from "next/link";
import Image from "next/image";
import LanguageSwitcher from "./languageSwitcher";

export default function LayoutMainHeader() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const { ready } = useTranslation("header");
  const router = useRouter();
  const headerRef = useRef<HTMLHeadElement>(null);

  useEffect(() => {
    setMounted(true);

    // Header scroll effect
    const handleScroll = () => {
      const scrollPosition = window.scrollY;
      setScrolled(scrollPosition > 50);
    };

    window.addEventListener("scroll", handleScroll);
    return () => window.removeEventListener("scroll", handleScroll);
  }, []);

  useEffect(() => {
    // Animate header on scroll - SOLO blur e shadow, NO cambio colore
    if (headerRef.current) {
      gsap.to(headerRef.current, {
        backdropFilter: scrolled ? "blur(25px)" : "blur(15px)",
        boxShadow: scrolled
          ? "0 8px 32px rgba(92, 77, 225, 0.15)"
          : "0 4px 16px rgba(92, 77, 225, 0.08)",
        duration: 0.4,
        ease: "power2.out",
      });
    }
  }, [scrolled]);

  // ✅ Renderizza sempre l'header, ma con fallback per i testi
  return (
    <header
      key={`header-${router.locale}-${ready}`}
      ref={headerRef}
      className="fixed top-0 left-0 w-full z-[100] bg-arcticWhite/20 dark:bg-polarNight/20 backdrop-blur-md border-b border-white/20 dark:border-white/10 text-polarNight dark:text-arcticWhite transition-all duration-300"
    >
      <div className="px-6 flex h-16 items-center justify-between">
        {/* Logo */}
        <Link href="/" className="flex items-center space-x-2 group">
          <div className="relative">
            <Image
              src="/logo/DataPolar_Logo.svg"
              alt="DataPolar Logo"
              width={35}
              height={35}
              priority
            />
          </div>
          <div className="relative overflow-hidden">
            <Image
              src="/logo/DataPolar_Lettering.svg"
              alt="DataPolar Lettering"
              width={125}
              height={30}
              priority
            />
          </div>
        </Link>

        {/* Right section */}
        <div className="flex items-center space-x-3" suppressHydrationWarning>
          <div
            className={
              mounted
                ? "opacity-0 animate-[fadeIn_0.5s_ease-in-out_0.3s_forwards]"
                : "opacity-0"
            }
          >
            <LanguageSwitcher />
          </div>

          {mounted && (
            <button
              onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
              className="p-2.5 rounded-xl hover:bg-white/20 dark:hover:bg-white/10 transition-all duration-300 group border border-white/20 dark:border-white/10 hover:border-coldIndigo/30 dark:hover:border-glacierBlue/30 opacity-0 animate-[fadeIn_0.5s_ease-in-out_0.4s_forwards]"
              aria-label="Toggle Theme"
            >
              <div className="relative w-5 h-5">
                {theme === "dark" ? (
                  <Sun className="w-5 h-5 transition-transform duration-300 group-hover:rotate-180 text-yellow-400" />
                ) : (
                  <Moon className="w-5 h-5 transition-transform duration-300 group-hover:rotate-12 text-indigo-600" />
                )}
              </div>
            </button>
          )}
        </div>
      </div>

      <style jsx>{`
        @keyframes fadeIn {
          from {
            opacity: 0;
            transform: translateY(-10px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
      `}</style>
    </header>
  );
}
