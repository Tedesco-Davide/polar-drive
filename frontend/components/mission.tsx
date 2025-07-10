"use client";

import { useTranslation } from "next-i18next";
import { useEffect, useRef } from "react";
import { useRouter } from "next/router";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/dist/ScrollTrigger";

// Register ScrollTrigger plugin
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function Mission() {
  const { t } = useTranslation("mission");
  const router = useRouter();
  const sectionRef = useRef<HTMLElement>(null);

  const navigateToPolarDrive = () => {
    router.push("/polardrive");
  };

  // Dati delle statistiche
  const statsData = [
    { number: 100, suffix: "%", labelKey: "stats.privacy_compliance" },
    { number: 24, suffix: "/7", labelKey: "stats.data_monitoring" },
    { number: 0, suffix: "", labelKey: "stats.data_leakage" },
    { number: 5, suffix: "★", labelKey: "stats.security_rating" },
  ];

  // Dati di Ricerca e Sviluppo
  const researchData = [
    {
      icon: "🧠",
      titleKey: "research_development.polar_ai.title",
      descKey: "research_development.polar_ai.description",
    },
    {
      icon: "⚡",
      titleKey: "research_development.innovative_architectures.title",
      descKey: "research_development.innovative_architectures.description",
    },
    {
      icon: "🔒",
      titleKey: "research_development.privacy_by_design.title",
      descKey: "research_development.privacy_by_design.description",
    },
    {
      icon: "🔬",
      titleKey: "research_development.innovation_lab.title",
      descKey: "research_development.innovation_lab.description",
    },
  ];

  // Dati degli ambiti di applicazione
  const applicationAreas = [
    {
      icon: "🚗",
      titleKey: "application_areas.mobility_transport.title",
      descKey: "application_areas.mobility_transport.description",
    },
    {
      icon: "⚡",
      titleKey: "application_areas.energy_sustainability.title",
      descKey: "application_areas.energy_sustainability.description",
    },
    {
      icon: "🔰️",
      titleKey: "application_areas.road_safety.title",
      descKey: "application_areas.road_safety.description",
    },
    {
      icon: "🏙️",
      titleKey: "application_areas.smart_cities.title",
      descKey: "application_areas.smart_cities.description",
    },
    {
      icon: "📈",
      titleKey: "application_areas.behavioral_marketing.title",
      descKey: "application_areas.behavioral_marketing.description",
    },
    {
      icon: "🏭",
      titleKey: "application_areas.industry_40.title",
      descKey: "application_areas.industry_40.description",
    },
    {
      icon: "🛒",
      titleKey: "application_areas.retail_distribution.title",
      descKey: "application_areas.retail_distribution.description",
    },
    {
      icon: "🏥",
      titleKey: "application_areas.health_wellness.title",
      descKey: "application_areas.health_wellness.description",
    },
    {
      icon: "🔍",
      titleKey: "application_areas.predictive_analysis.title",
      descKey: "application_areas.predictive_analysis.description",
    },
  ];

  // Dati delle certificazioni
  const certificationsData = [
    {
      icon: "🏛️",
      titleKey: "certifications.tech_company.title",
      descKey: "certifications.tech_company.description",
    },
    {
      icon: "🔐",
      titleKey: "certifications.gdpr_compliance.title",
      descKey: "certifications.gdpr_compliance.description",
    },
    {
      icon: "🔬",
      titleKey: "certifications.internal_rd.title",
      descKey: "certifications.internal_rd.description",
    },
  ];

  // Dati della sovranità dei dati
  const dataSovereigntyFeatures = [
    "🌍 " + t("data_sovereignty.features.local_ai"),
    "🚫 " + t("data_sovereignty.features.no_data_transmission"),
    "🔒 " + t("data_sovereignty.features.secure_documentation"),
    "🏠 " + t("data_sovereignty.features.dedicated_infrastructure"),
    "📊 " + t("data_sovereignty.features.auditable_process"),
    "🔰️️ " + t("data_sovereignty.features.privacy_design"),
  ];

  useEffect(() => {
    if (typeof window !== "undefined") {
      // Pulisce tutti i trigger esistenti prima di crearne di nuovi
      ScrollTrigger.getAll().forEach((trigger) => trigger.kill());

      // Aspetta un frame per assicurarsi che il DOM sia pronto
      requestAnimationFrame(() => {
        // Animazioni GSAP per tutti gli elementi
        gsap.utils
          .toArray<Element>(".animate-on-scroll")
          .forEach((element: Element) => {
            gsap.fromTo(
              element,
              { opacity: 0, y: 50 },
              {
                opacity: 1,
                y: 0,
                duration: 0.8,
                ease: "power3.out",
                scrollTrigger: {
                  trigger: element,
                  start: "top 85%",
                  end: "bottom 15%",
                  toggleActions: "play none none reverse",
                  refreshPriority: -1,
                },
              }
            );
          });

        // Animazione staggered per le card
        gsap.utils
          .toArray<Element>(".card-stagger")
          .forEach((element: Element, index: number) => {
            gsap.fromTo(
              element,
              { opacity: 0, y: 30, scale: 0.9 },
              {
                opacity: 1,
                y: 0,
                scale: 1,
                duration: 0.6,
                ease: "back.out(1.2)",
                delay: index * 0.1,
                scrollTrigger: {
                  trigger: element,
                  start: "top 85%",
                  toggleActions: "play none none reverse",
                  refreshPriority: -1,
                },
              }
            );
          });

        // Animazione per i numeri
        gsap.utils
          .toArray<HTMLElement>(".counter")
          .forEach((element: HTMLElement) => {
            const target = parseInt(
              element.getAttribute("data-target") || "0",
              10
            );
            gsap.fromTo(
              element,
              { textContent: 0 },
              {
                textContent: target,
                duration: 2,
                ease: "power2.out",
                snap: { textContent: 1 },
                scrollTrigger: {
                  trigger: element,
                  start: "top 80%",
                  refreshPriority: -1,
                },
              }
            );
          });

        // Refresh ScrollTrigger dopo aver impostato tutti i trigger
        ScrollTrigger.refresh();
      });
    }
  }, [router.locale]);

  // useEffect separato per monitorare i cambi di locale
  useEffect(() => {
    // Refresh ScrollTrigger quando la locale cambia
    setTimeout(() => {
      ScrollTrigger.refresh();
    }, 200);
  }, [router.locale]);

  return (
    <section
      ref={sectionRef}
      id="mission"
      className="relative w-full overflow-hidden py-12 px-6 scroll-mt-16 bg-gradient-to-bl from-indigo-100 via-blue-50 to-slate-50 dark:from-indigo-900 dark:via-blue-950 dark:to-indigo-950"
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

      <div className="relative z-20 max-w-7xl mx-auto">
        {/* Chi Siamo */}
        <div className="mb-20 animate-on-scroll">
          <h2 className="text-3xl md:text-5xl font-bold bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent text-center mb-8">
            {t("who_we_are.title")}
          </h2>
          <div className="max-w-4xl mx-auto text-center space-y-6">
            <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80">
              {t("who_we_are.description_1")}
            </p>
            <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80">
              {t("who_we_are.description_2")}
            </p>
          </div>
        </div>

        {/* Stats Section */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-8 mb-20">
          {statsData.map((stat, index) => (
            <div key={index} className="text-center card-stagger">
              <div className="h-full flex flex-col justify-center p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300">
                <div className="text-3xl md:text-4xl font-bold text-coldIndigo dark:text-glacierBlue mb-2">
                  <span className="counter" data-target={stat.number}>
                    {stat.number}
                  </span>
                  {stat.suffix}
                </div>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70">
                  {t(stat.labelKey)}
                </p>
              </div>
            </div>
          ))}
        </div>

        {/* Ricerca e Sviluppo */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-4xl font-bold text-center mb-12 text-coldIndigo dark:text-glacierBlue">
            {t("research_development.title")}
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            {researchData.map((item, index) => (
              <div
                key={index}
                className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
              >
                <div className="text-4xl mb-4">{item.icon}</div>
                <h4 className="text-xl font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {t(item.titleKey)}
                </h4>
                <p className="text-polarNight/70 dark:text-articWhite/70 leading-relaxed">
                  {t(item.descKey)}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Mission Statement */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-4xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            {t("mission_statement.title")}
          </h3>
          <div className="max-w-4xl mx-auto text-center">
            <blockquote className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-8 italic">
              &quot;{t("mission_statement.quote")}&quot;
            </blockquote>
          </div>
        </div>

        {/* Ambiti di Applicazione */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-12 text-coldIndigo dark:text-glacierBlue">
            {t("application_areas.title")}
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {applicationAreas.map((area, index) => (
              <div
                key={index}
                className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
              >
                <div className="text-3xl mb-4">{area.icon}</div>
                <h4 className="text-lg font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {t(area.titleKey)}
                </h4>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70 leading-relaxed">
                  {t(area.descKey)}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Partnerships */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            {t("partnerships.title")}
          </h3>
          <div className="max-w-4xl mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10">
                <h4 className="text-xl font-semibold mb-4 text-coldIndigo dark:text-glacierBlue">
                  {t("partnerships.strategic_collaborations.title")}
                </h4>
                <p className="text-polarNight/80 dark:text-articWhite/80 leading-relaxed">
                  {t("partnerships.strategic_collaborations.description")}
                </p>
              </div>
              <div className="p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10">
                <h4 className="text-xl font-semibold mb-4 text-coldIndigo dark:text-glacierBlue">
                  {t("partnerships.professional_studies.title")}
                </h4>
                <p className="text-polarNight/80 dark:text-articWhite/80 leading-relaxed">
                  {t("partnerships.professional_studies.description")}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Sovranità dei Dati */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            {t("data_sovereignty.title")}
          </h3>
          <div className="max-w-4xl mx-auto">
            <div className="p-8 bg-gradient-to-r from-coldIndigo/10 to-glacierBlue/10 backdrop-blur-sm rounded-2xl border border-coldIndigo/20">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {dataSovereigntyFeatures.map((feature, index) => (
                  <div key={index} className="flex items-center space-x-3">
                    <span className="text-2xl">{feature.slice(0, 2)}</span>
                    <span className="text-polarNight/80 dark:text-articWhite/80">
                      {feature.slice(3)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        {/* Certificazioni */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-12 text-coldIndigo dark:text-glacierBlue">
            {t("certifications.title")}
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {certificationsData.map((cert, index) => (
              <div
                key={index}
                className="card-stagger text-center p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300"
              >
                <div className="text-4xl mb-4">{cert.icon}</div>
                <h4 className="text-lg font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {t(cert.titleKey)}
                </h4>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70">
                  {t(cert.descKey)}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Call to Action finale */}
        <div className="text-center animate-on-scroll">
          <div className="p-8 bg-gradient-to-r from-coldIndigo/20 to-glacierBlue/20 backdrop-blur-sm rounded-2xl border border-coldIndigo/30">
            <h3 className="text-2xl md:text-3xl font-bold mb-4 text-coldIndigo dark:text-glacierBlue">
              {t("cta.title")}
            </h3>
            <p className="text-lg text-polarNight/80 dark:text-articWhite/80 mb-6">
              {t("cta.description")}
            </p>
            <button
              onClick={navigateToPolarDrive}
              className="px-8 py-4 bg-coldIndigo text-white font-semibold rounded-full hover:scale-105 transition-all duration-300 hover:shadow-xl hover:shadow-coldIndigo/30"
            >
              {t("cta.button")}
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
