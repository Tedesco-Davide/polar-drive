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
  const { t } = useTranslation("contacts");
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
      console.error(t("contact.error.generic"), err);
      alert(err instanceof Error ? err.message : t("contact.error.generic"));
    } finally {
      setLoading(false);
    }
  };

  return (
    <section
      ref={sectionRef}
      id="contacts"
      className="relative w-full overflow-hidden py-12 px-6 scroll-mt-16 bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-indigo-950 dark:via-blue-950 dark:to-indigo-900"
    >
      {/* SVG Background con Pattern Esagonale */}
      <svg
        className="fixed inset-0 w-screen h-screen z-0"
        viewBox="0 0 1920 1080"
        preserveAspectRatio="xMidYMid slice"
        style={{
          position: "fixed",
          top: 0,
          left: 0,
          width: "100vw",
          height: "100vh",
          zIndex: 0,
        }}
      >
        <defs>
          {/* Pattern esagonale per griglia tech */}
          <pattern
            id="hexGridMission"
            width="100"
            height="87"
            patternUnits="userSpaceOnUse"
          >
            <polygon
              points="50,0 93.3,25 93.3,62 50,87 6.7,62 6.7,25"
              fill="none"
              stroke="#3b82f6"
              strokeWidth="1"
              opacity="0.15"
            />
            <circle cx="50" cy="43.5" r="2" fill="#06b6d4" opacity="0.4" />
          </pattern>
        </defs>

        {/* Griglia esagonale di sfondo */}
        <rect width="100%" height="100%" fill="url(#hexGridMission)" />
      </svg>

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
        <div className="bg-white/5 dark:bg-white/5 backdrop-blur-xl rounded-2xl p-8 border border-gray-300 dark:border-white/10 shadow-2xl">
          <form ref={formRef} className="space-y-6" onSubmit={handleSubmit}>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-polarNight dark:text-glacierBlue">
                  {t("contact.label.name")}
                </label>
                <input
                  name="name"
                  type="text"
                  required
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-gray-300 dark:border-white/10 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-polarNight dark:text-glacierBlue">
                  {t("contact.label.email")}
                </label>
                <input
                  name="email"
                  type="email"
                  required
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-gray-300 dark:border-white/10 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-polarNight dark:text-glacierBlue">
                  {t("contact.label.company")}
                </label>
                <input
                  name="company"
                  type="text"
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-gray-300 dark:border-white/10 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>

              <div className="form-element space-y-2">
                <label className="block text-sm font-semibold text-left text-polarNight dark:text-glacierBlue">
                  {t("contact.label.website")}
                </label>
                <input
                  name="website"
                  type="text"
                  className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-gray-300 dark:border-white/10 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm"
                />
              </div>
            </div>

            <div className="form-element space-y-2">
              <label className="block text-sm font-semibold text-left text-polarNight dark:text-glacierBlue">
                {t("contact.label.message")}
              </label>
              <textarea
                name="message"
                rows={5}
                required
                className="w-full px-4 py-3 rounded-xl bg-white/10 dark:bg-white/5 border border-gray-300 dark:border-white/10 text-polarNight dark:text-softWhite placeholder:text-polarNight/50 dark:placeholder:text-softWhite/50 focus:outline-none focus:ring-2 focus:ring-coldIndigo/50 focus:border-coldIndigo transition-all duration-300 backdrop-blur-sm resize-none"
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
