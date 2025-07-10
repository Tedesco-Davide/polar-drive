"use client";

import { useState, useEffect, useRef } from "react";
import { useTheme } from "next-themes";
import { Sun, Moon, Menu, X } from "lucide-react";
import { useTranslation } from "next-i18next";
import { useRouter } from "next/router";
import Link from "next/link";
import Image from "next/image";
import { gsap } from "gsap";
import LanguageSwitcher from "./languageSwitcher";

export default function Header() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const { t, ready } = useTranslation("header"); // ✅ Aggiunto 'ready'
  const router = useRouter();
  const headerRef = useRef<HTMLHeadElement>(null);
  const mobileMenuRef = useRef<HTMLDivElement>(null);

  const navigation = [
    { label: "header.home", href: "/" },
    { label: "header.mission", href: "#mission" },
    { label: "header.polardrive", href: "/polardrive" },
    { label: "header.contacts", href: "#contacts" },
  ];

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

  const toggleMobileMenu = () => {
    setMenuOpen(!menuOpen);

    if (mobileMenuRef.current) {
      if (!menuOpen) {
        // Opening animation
        gsap.fromTo(
          mobileMenuRef.current,
          { opacity: 0, y: -20 },
          { opacity: 1, y: 0, duration: 0.3, ease: "power2.out" }
        );

        // Animate menu items
        const menuItems =
          mobileMenuRef.current.querySelectorAll(".mobile-nav-item");
        gsap.fromTo(
          menuItems,
          { opacity: 0, y: 20 },
          {
            opacity: 1,
            y: 0,
            duration: 0.3,
            stagger: 0.1,
            delay: 0.1,
            ease: "power2.out",
          }
        );
      } else {
        // Closing animation
        gsap.to(mobileMenuRef.current, {
          opacity: 0,
          y: -20,
          duration: 0.3,
          ease: "power2.in",
        });
      }
    }
  };

  // ✅ Funzione per ottenere il testo con fallback
  const getNavigationText = (label: string) => {
    if (!ready) {
      // Fallback statico durante il caricamento
      switch (label) {
        case "header.home":
          return "Home";
        case "header.mission":
          return "Mission";
        case "header.polardrive":
          return "PolarDrive™";
        case "header.contacts":
          return "Contattaci";
        default:
          return "";
      }
    }
    return t(label);
  };

  // ✅ Se non è montato, mostra un placeholder che matcha il server
  if (!mounted) {
    return (
      <header className="fixed top-0 left-0 w-full z-[100] bg-articWhite/20 dark:bg-polarNight/20 backdrop-blur-md border-b border-white/20 dark:border-white/10 text-polarNight dark:text-articWhite transition-all duration-300">
        <div className="container px-6 flex h-16 items-center justify-between">
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

          {/* Desktop Navigation - placeholder */}
          <nav className="hidden md:flex items-center gap-8">
            {navigation.map((item) => (
              <div
                key={item.label}
                className="relative text-sm font-semibold text-polarNight/90 dark:text-articWhite/90 px-3 py-2"
              >
                {getNavigationText(item.label)}
              </div>
            ))}
          </nav>

          {/* Right section - placeholder */}
          <div className="flex items-center space-x-3">
            <div className="opacity-0">
              <LanguageSwitcher />
            </div>
            <div className="p-2.5 rounded-xl border border-white/20 dark:border-white/10 opacity-0">
              <div className="w-5 h-5" />
            </div>
            <div className="md:hidden p-2.5 rounded-xl border border-white/20 dark:border-white/10 opacity-0">
              <div className="w-6 h-6" />
            </div>
          </div>
        </div>
      </header>
    );
  }

  return (
    <header
      ref={headerRef}
      className="fixed top-0 left-0 w-full z-[100] bg-articWhite/20 dark:bg-polarNight/20 backdrop-blur-md border-b border-white/20 dark:border-white/10 text-polarNight dark:text-articWhite transition-all duration-300"
    >
      <div className="container px-6 flex h-16 items-center justify-between">
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

        {/* Desktop Navigation */}
        <nav className="hidden md:flex items-center gap-8">
          {navigation.map((item, index) => {
            const isAnchor = item.href.startsWith("#");
            const anchorTarget = item.href;

            const handleClick = (e: React.MouseEvent) => {
              if (isAnchor) {
                e.preventDefault();
                const targetPath = `/${anchorTarget}`;
                if (router.pathname !== "/") {
                  router.push(targetPath);
                } else {
                  const element = document.querySelector(anchorTarget);
                  if (element) {
                    element.scrollIntoView({ behavior: "smooth" });
                  }
                }
              }
            };

            return isAnchor ? (
              <a
                key={item.label}
                href={`/${anchorTarget}`}
                onClick={handleClick}
                className="relative text-sm font-semibold text-polarNight/90 dark:text-articWhite/90 hover:text-coldIndigo dark:hover:text-glacierBlue transition-all duration-300 group px-3 py-2 rounded-lg hover:bg-white/20 dark:hover:bg-white/10"
                style={{ animationDelay: `${index * 0.1}s` }}
              >
                {getNavigationText(item.label)}{" "}
                {/* ✅ Usa la funzione con fallback */}
                <span className="absolute -bottom-1 left-3 w-0 h-0.5 bg-gradient-to-r from-coldIndigo to-glacierBlue transition-all duration-300 group-hover:w-[calc(100%-1.5rem)] rounded-full" />
              </a>
            ) : (
              <Link
                key={item.label}
                href={item.href}
                className="relative text-sm font-semibold text-polarNight/90 dark:text-articWhite/90 hover:text-coldIndigo dark:hover:text-glacierBlue transition-all duration-300 group px-3 py-2 rounded-lg hover:bg-white/20 dark:hover:bg-white/10"
                style={{ animationDelay: `${index * 0.1}s` }}
              >
                {getNavigationText(item.label)}{" "}
                {/* ✅ Usa la funzione con fallback */}
                <span className="absolute -bottom-1 left-3 w-0 h-0.5 bg-gradient-to-r from-coldIndigo to-glacierBlue transition-all duration-300 group-hover:w-[calc(100%-1.5rem)] rounded-full" />
              </Link>
            );
          })}
        </nav>

        {/* Right section */}
        <div className="flex items-center space-x-3">
          <div className="opacity-0 animate-[fadeIn_0.5s_ease-in-out_0.3s_forwards]">
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

          {/* Mobile Menu Button */}
          <button
            className="md:hidden p-2.5 rounded-xl hover:bg-white/20 dark:hover:bg-white/10 transition-all duration-300 border border-white/20 dark:border-white/10 hover:border-coldIndigo/30 dark:hover:border-glacierBlue/30 opacity-0 animate-[fadeIn_0.5s_ease-in-out_0.5s_forwards]"
            onClick={toggleMobileMenu}
            aria-label="Toggle menu"
          >
            <div className="relative w-6 h-6">
              {menuOpen ? (
                <X className="w-6 h-6 transition-transform duration-300 rotate-90" />
              ) : (
                <Menu className="w-6 h-6 transition-transform duration-300" />
              )}
            </div>
          </button>
        </div>
      </div>

      {/* Mobile menu */}
      <div
        ref={mobileMenuRef}
        className={`md:hidden absolute top-full left-0 w-full transition-all duration-300 ease-in-out ${
          menuOpen ? "opacity-100 visible" : "opacity-0 invisible"
        } bg-articWhite/95 dark:bg-polarNight/95 backdrop-blur-xl border-t border-white/20 dark:border-white/10`}
      >
        <div className="px-6 py-6 space-y-3">
          {navigation.map((item) => {
            const isAnchor = item.href.startsWith("#");
            const anchorTarget = item.href;

            const handleClick = (e: React.MouseEvent) => {
              e.preventDefault();
              setMenuOpen(false);

              if (isAnchor) {
                const targetPath = `/${anchorTarget}`;
                if (router.pathname !== "/") {
                  router.push(targetPath);
                } else {
                  const element = document.querySelector(anchorTarget);
                  if (element) {
                    element.scrollIntoView({ behavior: "smooth" });
                  }
                }
              } else {
                router.push(item.href);
              }
            };

            return (
              <a
                key={item.label}
                href={isAnchor ? `/${anchorTarget}` : item.href}
                onClick={handleClick}
                className="mobile-nav-item block text-base font-semibold text-polarNight dark:text-articWhite hover:text-coldIndigo dark:hover:text-glacierBlue transition-all duration-300 py-3 px-4 rounded-xl hover:bg-white/20 dark:hover:bg-white/10 border border-transparent hover:border-coldIndigo/20 dark:hover:border-glacierBlue/20"
              >
                {getNavigationText(item.label)}{" "}
                {/* ✅ Usa la funzione con fallback */}
              </a>
            );
          })}
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
