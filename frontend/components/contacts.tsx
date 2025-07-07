"use client";

import { useEffect, useState, useRef } from "react";
import { useTranslation } from "next-i18next";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/dist/ScrollTrigger";
import { logFrontendEvent } from "@/utils/logger";

// Register ScrollTrigger plugin
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function Contacts() {
  const { t } = useTranslation();
  const [mounted, setMounted] = useState(false);
  const [loading, setLoading] = useState(false);
  const sectionRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    setMounted(true);
    logFrontendEvent("ContactsForm", "INFO", "Contacts form mounted");
  }, []);

  useEffect(() => {
    if (mounted && titleRef.current && formRef.current) {
      // Title animation
      gsap.fromTo(
        titleRef.current,
        { opacity: 0, y: 50 },
        {
          opacity: 1,
          y: 0,
          duration: 0.8,
          ease: "power3.out",
          scrollTrigger: {
            trigger: titleRef.current,
            start: "top 80%",
            end: "bottom 20%",
            toggleActions: "play none none reverse",
          },
        }
      );

      // Form animation
      const formElements = formRef.current.querySelectorAll(".form-element");
      gsap.fromTo(
        formElements,
        { opacity: 0, y: 30, rotationX: 15 },
        {
          opacity: 1,
          y: 0,
          rotationX: 0,
          duration: 0.8,
          ease: "power3.out",
          stagger: 0.1,
          scrollTrigger: {
            trigger: formRef.current,
            start: "top 80%",
            end: "bottom 20%",
            toggleActions: "play none none reverse",
          },
        }
      );
    }
  }, [mounted]);

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

      if (response.ok) {
        logFrontendEvent(
          "ContactsForm",
          "INFO",
          "Contact form submitted",
          `Name: ${formData.name}, Email: ${formData.email}`
        );

        // Success animation
        gsap.to(formRef.current, {
          scale: 1.02,
          duration: 0.3,
          yoyo: true,
          repeat: 1,
          ease: "power2.inOut",
        });

        alert(t("contact.success"));
        form.reset();
      }
    } catch (err) {
      logFrontendEvent(
        "ContactsForm",
        "ERROR",
        "Failed to submit contact form",
        err instanceof Error ? err.message : String(err)
      );
      console.error(t("admin.genericApiError"), err);
      alert(err instanceof Error ? err.message : t("admin.genericApiError"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <section
      ref={sectionRef}
      id="contacts"
      className="relative w-full overflow-hidden py-12 px-6 scroll-mt-16"
    >
      {/* Animated Background */}
      <div className="absolute inset-0 z-0">
        {mounted && (
          <div className="absolute inset-0 bg-gradient-to-br from-coldIndigo/10 via-glacierBlue/5 to-transparent" />
        )}
        <div className="absolute top-1/3 left-1/4 w-64 h-64 bg-coldIndigo/20 rounded-full blur-3xl animate-pulse" />
        <div
          className="absolute bottom-1/3 right-1/4 w-96 h-96 bg-glacierBlue/20 rounded-full blur-3xl animate-pulse"
          style={{ animationDelay: "2s" }}
        />
      </div>

      {/* Grid Pattern */}
      {mounted && (
        <div className="absolute inset-0 z-5 bg-[length:40px_40px] bg-[radial-gradient(circle_at_1px_1px,rgba(92,77,225,0.1)_1px,transparent_0)] opacity-50" />
      )}

      {/* Content */}
      <div className="relative z-20 max-w-4xl mx-auto text-center text-polarNight dark:text-articWhite">
        <h2
          ref={titleRef}
          className="text-4xl md:text-5xl font-bold mb-6 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent"
        >
          {t("contact.title")}
        </h2>

        <p className="text-lg md:text-xl leading-relaxed mb-8 text-polarNight/80 dark:text-articWhite/80 max-w-2xl mx-auto">
          {t("contact.description")}
        </p>

        {/* Enhanced Form */}
        <div className="bg-white/5 dark:bg-white/5 backdrop-blur-xl rounded-2xl p-8 border border-white/10 shadow-2xl">
          <form ref={formRef} className="space-y-6" onSubmit={handleSubmit}>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-coldIndigo">
                  {t("contact.label.name")}
                </label>
                <input
                  name="name"
                  type="text"
                  required
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-white/20 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-coldIndigo">
                  {t("contact.label.email")}
                </label>
                <input
                  name="email"
                  type="email"
                  required
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-white/20 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-coldIndigo">
                  {t("contact.label.company")}
                </label>
                <input
                  name="company"
                  type="text"
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-white/20 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-coldIndigo">
                  {t("contact.label.website")}
                </label>
                <input
                  name="website"
                  type="text"
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-white/20 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>
            </div>

            <div className="form-element space-y-2">
              <label className="block text-sm font-semibold text-left text-coldIndigo">
                {t("contact.label.message")}
              </label>
              <textarea
                name="message"
                rows={5}
                required
                className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-white/20 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm resize-none"
              />
            </div>

            <div className="form-element flex justify-center pt-4">
              <button
                type="submit"
                disabled={loading}
                className="group relative px-8 py-4 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-lg hover:shadow-coldIndigo/25 disabled:opacity-50 disabled:cursor-not-allowed overflow-hidden"
              >
                <span className="relative z-10 flex items-center gap-2">
                  {loading ? t("contact.loading") : t("contact.submit")}
                  {!loading && (
                    <span className="transition-transform duration-300 group-hover:translate-x-1">
                      →
                    </span>
                  )}
                </span>
              </button>
            </div>
          </form>
        </div>
      </div>

      <style jsx>{`
        @keyframes shine {
          0% {
            transform: translateX(-100%) skewX(-12deg);
          }
          100% {
            transform: translateX(200%) skewX(-12deg);
          }
        }
      `}</style>
    </section>
  );
}
