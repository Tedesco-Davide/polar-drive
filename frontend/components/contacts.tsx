"use client";

import { useEffect, useState } from "react";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import classNames from "classnames";

export default function Contacts() {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    const form = e.currentTarget;

    const formData = {
      name: (form.elements.namedItem("name") as HTMLInputElement).value,
      email: (form.elements.namedItem("email") as HTMLInputElement).value,
      company: (form.elements.namedItem("company") as HTMLInputElement).value,
      website: (form.elements.namedItem("website") as HTMLInputElement).value,
      message: (form.elements.namedItem("message") as HTMLTextAreaElement)
        .value,
    };

    if (
      !formData.name.trim() ||
      !formData.email.trim() ||
      !formData.message.trim()
    ) {
      alert(t("contact.error.required"));
      return;
    }
    try {
      setLoading(true);

      const response = await fetch(
        "https://script.google.com/macros/s/AKfycbxdP69YvO6rR07u7GEojGRDg-oJwoyn6QbKX6PLPyD6GP_wYMcptPxsKKC7nwJERQGC/exec",
        {
          method: "POST",
          body: JSON.stringify(formData),
        }
      );

      const result = await response.json();
      console.log("FORM SUBMISSION RESULT:", result);

      if (response.ok) {
        alert(t("contact.success"));
        form.reset();
      } else {
        alert(t("contact.error.send"));
      }
    } catch (err) {
      console.error(t("admin.genericApiError"), err);
      alert(err instanceof Error ? err.message : t("admin.genericApiError"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <section
      id="contacts"
      className="relative w-full overflow-hidden min-h-screen py-24 px-6 scroll-mt-16"
    >
      {/* Griglia di sfondo dinamica */}
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
        <div className="absolute left-1/2 top-32 -translate-x-1/2 w-[600px] h-[600px] rounded-full bg-[#5c4de14a] dark:bg-[#5c4de130] blur-3xl opacity-60" />
      </div>

      {/* Contenuto */}
      <div className="relative z-20 max-w-3xl mx-auto text-center text-polarNight dark:text-articWhite space-y-6">
        <h2 className="text-4xl md:text-5xl font-bold">{t("contact.title")}</h2>
        <p className="text-lg leading-relaxed">{t("contact.description")}</p>

        {/* Form card */}
        <div className="mt-12 bg-softWhite dark:bg-polarNight shadow-lg rounded-xl p-8 space-y-6 text-left">
          <form className="space-y-4" onSubmit={handleSubmit}>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium mb-1">
                  {t("contact.label.name")}
                </label>
                <input
                  name="name"
                  type="text"
                  required
                  placeholder={t("contact.placeholder.name")}
                  className="w-full px-4 py-2 rounded-md border border-gray-300 dark:border-gray-700 bg-softWhite dark:bg-transparent text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-coldIndigo"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">
                  {t("contact.label.email")}
                </label>
                <input
                  name="email"
                  type="email"
                  required
                  placeholder={t("contact.placeholder.email")}
                  className="w-full px-4 py-2 rounded-md border border-gray-300 dark:border-gray-700 bg-softWhite dark:bg-transparent text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-coldIndigo"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">
                  {t("contact.label.company")}
                </label>
                <input
                  name="company"
                  type="text"
                  placeholder={t("contact.placeholder.company")}
                  className="w-full px-4 py-2 rounded-md border border-gray-300 dark:border-gray-700 bg-softWhite dark:bg-transparent text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-coldIndigo"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">
                  {t("contact.label.website")}
                </label>
                <input
                  name="website"
                  type="text"
                  placeholder={t("contact.placeholder.website")}
                  className="w-full px-4 py-2 rounded-md border border-gray-300 dark:border-gray-700 bg-softWhite dark:bg-transparent text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-coldIndigo"
                />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">
                {t("contact.label.message")}
              </label>
              <textarea
                name="message"
                rows={5}
                required
                placeholder={t("contact.placeholder.message")}
                className="w-full px-4 py-2 rounded-md border border-gray-300 dark:border-gray-700 bg-softWhite dark:bg-transparent text-polarNight dark:text-softWhite focus:outline-none focus:ring-2 focus:ring-coldIndigo"
              />
            </div>
            <div className="flex justify-center">
              <button
                type="submit"
                disabled={loading}
                className="mt-4 w-full sm:w-auto px-6 py-3 rounded-full bg-coldIndigo text-softWhite font-semibold hover:bg-indigo-600 transition"
              >
                {loading ? t("contact.loading") : t("contact.submit")}
              </button>
            </div>
          </form>
        </div>
      </div>
    </section>
  );
}
