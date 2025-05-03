"use client";

import { useState, useEffect } from "react";
import { useTheme } from "next-themes";
import { Sun, Moon, Menu, X } from "lucide-react";
import { useTranslation } from "next-i18next";
import { useRouter } from "next/router";
import Link from "next/link";
import Image from "next/image";
import LanguageSwitcher from "./languageSwitcher";
import Chip from "./chip";

export default function Header() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const { t } = useTranslation();
  const router = useRouter();

  const navigation = [
    { label: "header.home", href: "/" },
    { label: "header.mission", href: "#mission" },
    { label: "header.products", href: "/products" },
    { label: "header.contacts", href: "#contacts" },
  ];

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <header className="fixed top-0 left-0 w-full z-[100] backdrop-blur bg-white/30 dark:bg-polarNight/30 text-polarNight dark:text-articWhite">
      <div className="container px-6 flex h-16 items-center justify-between">
        {/* Logo */}
        <Chip className="bg-polarNight border-none hover:bg-polarNight dark:hover:bg-polarNight">
          <Link href="/" className="flex items-center space-x-2">
            <Image
              src="/logo/DataPolar_Logo_Big.png"
              alt="DataPolar Logo"
              width={28}
              height={28}
              priority
            />
            <span className="ftext-sm md:text-2xl font-bold tracking-wide text-softWhite">
              DataPolar
            </span>
          </Link>
        </Chip>

        {/* Desktop Nav */}
        <nav className="hidden md:flex items-center gap-8">
          {navigation.map((item) => {
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
                className="text-sm font-bold text-gray-700 dark:text-gray-300 hover:text-black dark:hover:text-softWhite transition"
              >
                {t(item.label)}
              </a>
            ) : (
              <Link
                key={item.label}
                href={item.href}
                className="text-sm font-bold text-gray-700 dark:text-gray-300 hover:text-black dark:hover:text-softWhite transition"
              >
                {t(item.label)}
              </Link>
            );
          })}
        </nav>

        {/* Right section */}
        <div className="flex items-center space-x-3">
          <LanguageSwitcher />

          {mounted && (
            <button
              onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
              className="ml-2 p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-800"
              aria-label="Toggle Theme"
            >
              {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
            </button>
          )}

          <button
            className="md:hidden p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800"
            onClick={() => setMenuOpen(!menuOpen)}
            aria-label="Toggle menu"
          >
            {menuOpen ? <X size={20} /> : <Menu size={20} />}
          </button>
        </div>
      </div>

      {/* Mobile menu */}
      <div
        className={`md:hidden overflow-hidden transition-all duration-300 ease-in-out ${
          menuOpen ? "max-h-[300px] opacity-100 py-6" : "max-h-0 opacity-0 py-0"
        } px-6 flex flex-col items-center space-y-6 text-center`}
      >
        {navigation.map((item) => {
          const isAnchor = item.href.startsWith("#");
          const anchorTarget = item.href;

          const handleClick = (e: React.MouseEvent) => {
            e.preventDefault();
            setMenuOpen(false);
            const targetPath = `/${anchorTarget}`;
            if (isAnchor) {
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

          return isAnchor ? (
            <a
              key={item.label}
              href={`/${anchorTarget}`}
              onClick={handleClick}
              className="text-base font-semibold text-polarNight dark:text-softWhite hover:text-black dark:hover:text-softWhite transition"
            >
              {t(item.label)}
            </a>
          ) : (
            <a
              key={item.label}
              href={item.href}
              onClick={handleClick}
              className="text-base font-semibold text-polarNight dark:text-softWhite hover:text-black dark:hover:text-softWhite transition"
            >
              {t(item.label)}
            </a>
          );
        })}
      </div>
    </header>
  );
}
