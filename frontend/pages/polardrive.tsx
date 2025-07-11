"use client";

import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import { useEffect, useState, useRef } from "react";
import { useRouter } from "next/router";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/dist/ScrollTrigger";
import { logFrontendEvent } from "@/utils/logger";
import Head from "next/head";
import Header from "@/components/header";
import Image from "next/image";
import {
  ArrowRight,
  Brain,
  Battery,
  Radar,
  Shield,
  FileText,
  Building2,
  Truck,
  Zap,
  Lock,
  CheckCircle,
  Users,
  Award,
  TrendingUp,
  Briefcase,
  Crown,
  Gem,
  Landmark,
  Compass,
} from "lucide-react";

// Register GSAP plugins
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function PolarDrivePage() {
  const { t, ready } = useTranslation("polardrive");
  const router = useRouter();
  const [mounted, setMounted] = useState(false);
  const heroRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const quoteRef = useRef<HTMLParagraphElement>(null);
  const particlesRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setMounted(true);
    logFrontendEvent(
      "PolarDrivePage",
      "INFO",
      "PolarDrive product page loaded",
      `Lang: ${router.locale}, URL: ${window.location.href}`
    );

    // Create particle system
    createParticleSystem();

    return () => {
      ScrollTrigger.getAll().forEach((trigger) => trigger.kill());
    };
  }, [router.locale]);

  useEffect(() => {
    if (mounted && ready) {
      // ✅ Pulisce tutti i trigger esistenti prima di crearne di nuovi
      ScrollTrigger.getAll().forEach((trigger) => trigger.kill());

      // ✅ Aspetta un frame per assicurarsi che il DOM sia pronto
      requestAnimationFrame(() => {
        // Animate elements on scroll
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

        // Staggered animations for cards
        gsap.utils
          .toArray<Element>(".card-stagger")
          .forEach((element: Element, index: number) => {
            gsap.fromTo(
              element,
              { opacity: 0, y: 30, scale: 0.95 },
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

        // ✅ Refresh ScrollTrigger dopo aver impostato tutti i trigger
        ScrollTrigger.refresh();
      });

      // Hero animations
      if (titleRef.current && quoteRef.current) {
        const tl = gsap.timeline();

        gsap.set([titleRef.current, quoteRef.current], {
          opacity: 0,
          y: 60,
        });

        tl.to(titleRef.current, {
          opacity: 1,
          y: 0,
          duration: 0.8,
          ease: "power3.out",
        }).to(
          quoteRef.current,
          {
            opacity: 1,
            y: 0,
            duration: 0.6,
            ease: "power3.out",
          },
          "-=0.4"
        );
      }
    }
  }, [mounted, ready, router.locale]);

  // ✅ Nuovo useEffect per gestire i cambi di rotta
  useEffect(() => {
    const handleRouteChange = () => {
      setTimeout(() => {
        ScrollTrigger.refresh();
      }, 100);
    };

    router.events.on("routeChangeComplete", handleRouteChange);

    return () => {
      router.events.off("routeChangeComplete", handleRouteChange);
    };
  }, [router]);

  // ✅ useEffect separato per monitorare i cambi di locale
  useEffect(() => {
    if (ready) {
      setTimeout(() => {
        ScrollTrigger.refresh();
      }, 200);
    }
  }, [ready, router.locale]);

  const createParticleSystem = () => {
    if (!particlesRef.current) return;

    const particleCount = 25;
    const container = particlesRef.current;

    container.innerHTML = "";

    for (let i = 0; i < particleCount; i++) {
      const particle = document.createElement("div");
      particle.className = "particle";
      particle.style.cssText = `
        position: absolute;
        width: 2px;
        height: 2px;
        background: rgba(167, 198, 237, 0.6);
        border-radius: 50%;
        left: ${Math.random() * 100}%;
        animation: particle-float ${Math.random() * 15 + 10}s linear infinite;
        animation-delay: ${Math.random() * 25}s;
      `;
      container.appendChild(particle);
    }
  };

  const scrollToContacts = () => {
    const anchorTarget = "#contacts";

    if (router.pathname !== "/") {
      window.location.href = `/${anchorTarget}`;
    } else {
      const element = document.querySelector(anchorTarget);
      if (element) {
        element.scrollIntoView({ behavior: "smooth" });
      }
    }
  };

  // ✅ LOADING STATE CORRETTO - Nessuna chiamata a t() qui
  if (!mounted || !ready) {
    return (
      <>
        <Head>
          <title>PolarDrive™</title>
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <link rel="icon" href="/favicon.ico" />
        </Head>

        {/* AGGIUNGI QUESTO GRADIENTE ANCHE QUI! */}
        <div className="fixed inset-0 bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-950 dark:via-blue-950 dark:to-indigo-900 z-0" />

        {/* SVG Background */}
        <svg
          className="fixed inset-0 w-screen h-screen z-10"
          viewBox="0 0 1920 1080"
          preserveAspectRatio="xMidYMid slice"
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            width: "100vw",
            height: "100vh",
            zIndex: 10,
          }}
        >
          <defs>
            <pattern
              id="hexGridMission"
              width="100"
              height="87"
              patternUnits="userSpaceOnUse"
            >
              <polygon
                points="50,0 93.3,25 93.3,62 50,87 6.7,62 6.7,25"
                fill="transparent"
                stroke="#3b82f6"
                strokeWidth="1"
                opacity="0.15"
              />
              <circle cx="50" cy="43.5" r="2" fill="#06b6d4" opacity="0.4" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#hexGridMission)" />
        </svg>

        {/* Particle System Background */}
        <div
          ref={particlesRef}
          className="fixed inset-0 pointer-events-none z-20"
        />
      </>
    );
  }

  // ✅ CONTENUTO PRINCIPALE - Solo qui usiamo t()
  const benefits = [
    {
      icon: Brain,
      title: t("benefits.automated_analysis.title"),
      description: t("benefits.automated_analysis.description"),
    },
    {
      icon: Battery,
      title: t("benefits.ecological_transition.title"),
      description: t("benefits.ecological_transition.description"),
    },
    {
      icon: Radar,
      title: t("benefits.territory_monitoring.title"),
      description: t("benefits.territory_monitoring.description"),
    },
    {
      icon: Shield,
      title: t("benefits.data_protection.title"),
      description: t("benefits.data_protection.description"),
    },
    {
      icon: FileText,
      title: t("benefits.document_value.title"),
      description: t("benefits.document_value.description"),
    },
    {
      icon: TrendingUp,
      title: t("benefits.predictive_intelligence.title"),
      description: t("benefits.predictive_intelligence.description"),
    },
  ];

  const targetAudience = [
    {
      icon: Building2,
      title: t("target_audience.industrial_logistics.title"),
      description: t("target_audience.industrial_logistics.description"),
    },
    {
      icon: Zap,
      title: t("target_audience.sustainable_mobility.title"),
      description: t("target_audience.sustainable_mobility.description"),
    },
    {
      icon: Truck,
      title: t("target_audience.fleet_managers.title"),
      description: t("target_audience.fleet_managers.description"),
    },
    {
      icon: Landmark,
      title: t("target_audience.public_entities.title"),
      description: t("target_audience.public_entities.description"),
    },
    {
      icon: Briefcase,
      title: t("target_audience.holdings.title"),
      description: t("target_audience.holdings.description"),
    },
    {
      icon: Users,
      title: t("target_audience.investment_companies.title"),
      description: t("target_audience.investment_companies.description"),
    },
    {
      icon: Crown,
      title: t("target_audience.corporations.title"),
      description: t("target_audience.corporations.description"),
    },
    {
      icon: Gem,
      title: t("target_audience.family_office.title"),
      description: t("target_audience.family_office.description"),
    },
  ];

  const complianceFeatures = [
    t("compliance.gdpr_conformity"),
    t("compliance.data_protection_design"),
    t("compliance.aggregated_treatment"),
    t("compliance.consent_collection"),
  ];

  const securityFeatures = [
    t("security.encrypted_interactions"),
    t("security.lightweight_engine"),
    t("security.cyber_hygiene"),
    t("security.protection_violations"),
  ];

  return (
    <>
      <Head>
        <title>PolarDrive™</title>
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <link rel="icon" href="/favicon.ico" />
      </Head>

      <div className="fixed inset-0 bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-950 dark:via-blue-950 dark:to-indigo-900 z-0" />

      {/* SVG Background */}
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
          <pattern
            id="hexGridMission"
            width="100"
            height="87"
            patternUnits="userSpaceOnUse"
          >
            <polygon
              points="50,0 93.3,25 93.3,62 50,87 6.7,62 6.7,25"
              fill="transparent"
              stroke="#3b82f6"
              strokeWidth="1"
              opacity="0.15"
            />
            <circle cx="50" cy="43.5" r="2" fill="#06b6d4" opacity="0.4" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#hexGridMission)" />
      </svg>

      {/* Particle System Background */}
      <div
        ref={particlesRef}
        className="fixed inset-0 pointer-events-none z-20"
      />

      {/* Main Content */}
      <div className="relative z-30">
        <Header />

        {/* Hero Section */}
        <section
          ref={heroRef}
          className="relative w-full overflow-hidden min-h-screen flex items-center pt-16"
        >
          <div className="relative z-20 container mx-auto pt-5 pb-20 lg:p-20">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 md:gap-12">
              {/* Text Content */}
              <div className="space-y-8">
                <div>
                  <h1
                    ref={titleRef}
                    className="text-4xl md:text-6xl lg:text-7xl font-bold mb-6 leading-tight"
                  >
                    <span className="bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-clip-text text-transparent bg-[length:200%_200%] animate-gradient-x">
                      PolarDrive™
                    </span>
                  </h1>

                  {/* Quote */}
                  <div className="relative">
                    <div className="absolute -left-4 top-0 w-1 h-full bg-gradient-to-b from-coldIndigo to-glacierBlue rounded-full" />
                    <blockquote className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-8 italic pl-8 relative">
                      <span className="text-6xl text-coldIndigo/30 absolute -top-4 -left-2">
                        &quot;
                      </span>
                      {t("hero.quote")}
                      <span className="text-6xl text-coldIndigo/30 absolute -bottom-8 right-0">
                        &quot;
                      </span>
                    </blockquote>
                  </div>
                </div>

                <div className="space-y-6">
                  {/* Description */}
                  <div className="relative p-6 bg-gradient-to-r from-white/10 to-white/5 backdrop-blur-sm rounded-2xl border border-gray-300 dark:border-white/10">
                    <p className="text-lg md:text-xl leading-relaxed text-polarNight/90 dark:text-articWhite/90">
                      {t("hero.description")}
                    </p>
                  </div>
                </div>
              </div>

              {/* Product Image */}
              <div className="relative mt-8 lg:mt-0">
                <div className="relative h-full">
                  <div className="absolute inset-0 bg-gradient-to-br from-coldIndigo/30 to-glacierBlue/30 rounded-3xl blur-2xl animate-pulse" />
                  <div className="relative w-full h-64 md:h-80 lg:h-full bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-3xl flex items-center justify-center backdrop-blur-sm border border-gray-300 dark:border-white/10 overflow-hidden lg:mb-0">
                    <div className="absolute inset-0 bg-[length:40px_40px] bg-[linear-gradient(to_right,rgba(59,130,246,0.15)_1px,transparent_1px),linear-gradient(to_bottom,rgba(59,130,246,0.15)_1px,transparent_1px)] dark:bg-[linear-gradient(to_right,rgba(167,198,237,0.08)_1px,transparent_1px),linear-gradient(to_bottom,rgba(167,198,237,0.08)_1px,transparent_1px)] animate-[gridPulse_4s_ease-in-out_infinite]" />

                    <div className="relative w-full h-full p-6 md:p-8">
                      <Image
                        src="/logo/PolarDrive_Logo.svg"
                        alt="PolarDrive Logo"
                        fill
                        className="object-contain drop-shadow-2xl"
                        priority
                      />
                    </div>

                    {/* Floating elements */}
                    <div
                      className="absolute top-6 left-6 w-6 h-6 border-2 border-coldIndigo/40 rounded-full animate-spin"
                      style={{ animationDuration: "8s" }}
                    />
                    <div
                      className="absolute top-12 right-8 w-4 h-4 bg-glacierBlue/40 rounded-full animate-bounce"
                      style={{ animationDelay: "1s", animationDuration: "2s" }}
                    />
                    <div
                      className="absolute bottom-8 left-12 w-8 h-8 border border-coldIndigo/30 rotate-45 animate-pulse"
                      style={{ animationDelay: "2s" }}
                    />
                    <div
                      className="absolute bottom-6 right-6 w-5 h-5 bg-gradient-to-r from-coldIndigo/40 to-glacierBlue/40 rounded-full animate-ping"
                      style={{ animationDelay: "0.5s" }}
                    />
                    <div
                      className="absolute top-1/4 right-1/4 w-3 h-3 bg-articWhite/30 rounded-full animate-pulse"
                      style={{ animationDelay: "3s" }}
                    />
                    <div
                      className="absolute bottom-1/3 left-1/4 w-2 h-8 bg-gradient-to-b from-coldIndigo/40 to-transparent rounded-full animate-pulse"
                      style={{ animationDelay: "1.5s" }}
                    />
                  </div>

                  <div
                    className="absolute -top-4 -right-4 bg-white/10 backdrop-blur-sm rounded-2xl border border-coldIndigo/30 p-4"
                    style={{ animationDuration: "3s", animationDelay: "2s" }}
                  >
                    <div className="text-coldIndigo dark:text-glacierBlue font-bold text-lg">
                      AI
                    </div>
                    <div className="text-xs text-polarNight/60 dark:text-articWhite/60">
                      {t("hero.ai_powered")}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* What is PolarDrive */}
            <div className="relative mt-10">
              <div className="absolute inset-0 bg-gradient-to-r from-coldIndigo/5 to-glacierBlue/5 rounded-3xl blur-xl" />
              <div className="relative border border-coldIndigo/20 bg-white/5 backdrop-blur-sm rounded-3xl p-8">
                <div className="flex items-center gap-4 mb-6">
                  <h2 className="text-2xl md:text-3xl font-bold text-coldIndigo dark:text-glacierBlue">
                    {t("what_is.title")}
                  </h2>
                </div>

                <div className="space-y-4 text-base md:text-lg leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                  <p className="flex items-start gap-3">
                    <span className="w-2 h-2 bg-coldIndigo rounded-full mt-3 flex-shrink-0" />
                    {t("what_is.point1")}
                  </p>
                  <p className="flex items-start gap-3">
                    <span className="w-2 h-2 bg-glacierBlue rounded-full mt-3 flex-shrink-0" />
                    {t("what_is.point2")}
                  </p>
                  <p className="flex items-start gap-3">
                    <span className="w-2 h-2 bg-coldIndigo rounded-full mt-3 flex-shrink-0" />
                    {t("what_is.point3")}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Vision Section */}
        <section className="relative w-full overflow-hidden pt-10 pb-8 md:pt-24 md:pb-24 px-6">
          <div className="relative z-20 max-w-5xl mx-auto text-center animate-on-scroll">
            <h2 className="text-3xl md:leading-relaxed md:text-5xl font-bold mb-8 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent">
              {t("vision.title")}
            </h2>

            <div className="p-8 bg-gradient-to-r from-coldIndigo/10 to-glacierBlue/10 backdrop-blur-sm rounded-3xl border border-coldIndigo/20">
              <p className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-6">
                {t("vision.subtitle")}
              </p>
              <p className="text-lg leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                {t("vision.description")}
              </p>
            </div>
          </div>
        </section>

        {/* Benefits Section */}
        <section className="relative w-full overflow-hidden pt-5 pb-5 md:pt-24 md:pb-24 px-6">
          <div className="relative z-20 max-w-7xl mx-auto">
            <h2 className="text-3xl md:text-5xl font-bold text-center mb-12 md:mb-16 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent animate-on-scroll">
              {t("benefits.title")}
            </h2>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
              {benefits.map((benefit, index) => {
                const Icon = benefit.icon;
                return (
                  <div
                    key={index}
                    className="card-stagger p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
                  >
                    <div className="w-16 h-16 mb-6 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-2xl flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                      <Icon className="w-8 h-8 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h3 className="text-xl font-bold mb-4 text-coldIndigo dark:text-glacierBlue">
                      {benefit.title}
                    </h3>
                    <p className="text-polarNight/70 dark:text-articWhite/70 leading-relaxed">
                      {benefit.description}
                    </p>
                  </div>
                );
              })}
            </div>
          </div>
        </section>

        {/* Target Audience Section */}
        <section className="relative w-full overflow-hidden pt-10 pb-8 md:pt-24 md:pb-24 px-6">
          <div className="relative z-20 max-w-6xl mx-auto">
            <h2 className="text-3xl md:text-5xl font-bold text-center mb-12 md:mb-16 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent animate-on-scroll">
              {t("target_audience.title")}
            </h2>

            <p className="text-lg text-center mb-12 text-polarNight/80 dark:text-articWhite/80 max-w-4xl mx-auto animate-on-scroll">
              {t("target_audience.description")}
            </p>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {targetAudience.map((target, index) => {
                const Icon = target.icon;
                return (
                  <div
                    key={index}
                    className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group text-center"
                  >
                    <div className="w-12 h-12 mx-auto mb-4 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-xl flex items-center justify-center group-hover:scale-110 transition-transform duration-300">
                      <Icon className="w-6 h-6 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h4 className="text-lg font-semibold mb-2 text-coldIndigo dark:text-glacierBlue">
                      {target.title}
                    </h4>
                    <p className="text-sm text-polarNight/70 dark:text-articWhite/70">
                      {target.description}
                    </p>
                  </div>
                );
              })}
            </div>
          </div>
        </section>

        {/* Compliance Section */}
        <section className="relative w-full overflow-hidden pt-5 pb-20 md:pt-24 md:pb-24 px-6">
          <div className="relative z-20 max-w-6xl mx-auto">
            <h2 className="text-3xl md:leading-relaxed md:text-5xl font-bold text-center mb-12 md:mb-12 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent animate-on-scroll leading-6">
              {t("compliance.card_title")}
            </h2>
            <p className="text-lg text-center mb-12 text-polarNight/80 dark:text-articWhite/80 max-w-4xl mx-auto animate-on-scroll">
              {t("compliance.card_desciption")}
            </p>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-12">
              {/* Legal Compliance */}
              <div className="animate-on-scroll">
                <div className="p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-gray-300 dark:border-white/10">
                  <div className="flex items-center mb-6">
                    <div className="w-12 h-12 mr-4 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-xl flex items-center justify-center">
                      <Award className="w-6 h-6 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h3 className="text-2xl font-bold text-coldIndigo dark:text-glacierBlue">
                      {t("compliance.title")}
                    </h3>
                  </div>
                  <p className="text-polarNight/80 dark:text-articWhite/80 mb-6">
                    {t("compliance.description")}
                  </p>
                  <ul className="space-y-3">
                    {complianceFeatures.map((feature, index) => (
                      <li key={index} className="flex items-start space-x-3">
                        <CheckCircle className="w-5 h-5 text-green-500 mt-0.5 flex-shrink-0" />
                        <span className="text-sm text-polarNight/70 dark:text-articWhite/70">
                          {feature}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>

              {/* Security */}
              <div className="animate-on-scroll">
                <div className="h-full p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-gray-300 dark:border-white/10">
                  <div className="flex items-center mb-6">
                    <div className="w-12 h-12 mr-4 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-xl flex items-center justify-center">
                      <Lock className="w-6 h-6 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h3 className="text-2xl font-bold text-coldIndigo dark:text-glacierBlue">
                      {t("security.title")}
                    </h3>
                  </div>
                  <p className="text-polarNight/80 dark:text-articWhite/80 mb-6">
                    {t("security.description")}
                  </p>
                  <ul className="space-y-3">
                    {securityFeatures.map((feature, index) => (
                      <li key={index} className="flex items-start space-x-3">
                        <CheckCircle className="w-5 h-5 text-green-500 mt-0.5 flex-shrink-0" />
                        <span className="text-sm text-polarNight/70 dark:text-articWhite/70">
                          {feature}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Final CTA Section */}
        <section className="relative w-full overflow-hidden py-24 px-6">
          <div className="absolute inset-0 z-0">
            <div className="absolute inset-0" />
          </div>

          <div className="relative z-20 max-w-4xl mx-auto text-center animate-on-scroll">
            <div className="p-12 bg-gradient-to-r from-coldIndigo/20 to-glacierBlue/20 backdrop-blur-sm rounded-3xl border border-coldIndigo/30">
              <Compass className="w-16 h-16 mx-auto mb-6 text-coldIndigo dark:text-glacierBlue" />
              <h3 className="text-3xl md:text-4xl font-bold mb-6 text-coldIndigo dark:text-glacierBlue">
                {t("cta.title")}
              </h3>
              <p className="text-lg text-polarNight/80 dark:text-articWhite/80 mb-8 max-w-2xl mx-auto">
                {t("cta.description")}
              </p>
              <button
                onClick={scrollToContacts}
                className="inline-flex items-center gap-3 px-10 py-5 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-xl hover:shadow-coldIndigo/30 group"
              >
                <span>{t("cta.button")}</span>
                <ArrowRight className="w-5 h-5 transition-transform duration-300 group-hover:translate-x-1" />
              </button>
            </div>
          </div>
        </section>
      </div>

      {/* CSS Animations */}
      <style jsx global>{`
        @keyframes particle-float {
          0% {
            transform: translateY(100vh) rotate(0deg);
            opacity: 0;
          }
          10% {
            opacity: 0.6;
          }
          90% {
            opacity: 0.6;
          }
          100% {
            transform: translateY(-100vh) rotate(360deg);
            opacity: 0;
          }
        }

        .particle {
          pointer-events: none;
        }

        * {
          -webkit-font-smoothing: antialiased;
          -moz-osx-font-smoothing: grayscale;
        }

        button:focus-visible,
        a:focus-visible {
          outline: 2px solid #5c4de1;
          outline-offset: 2px;
        }

        .bg-gradient-radial {
          background: radial-gradient(circle, var(--tw-gradient-stops));
        }

        @media (max-width: 768px) {
          .particle {
            display: none;
          }
        }

        @media (prefers-reduced-motion: reduce) {
          .particle,
          .animate-pulse {
            animation: none !important;
          }
        }

        @keyframes gradient-x {
          0%,
          100% {
            background-position: 0% 50%;
          }
          50% {
            background-position: 100% 50%;
          }
        }

        @keyframes fade-in {
          from {
            opacity: 0;
            transform: translateY(10px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }

        .animate-gradient-x {
          animation: gradient-x 6s ease infinite;
        }

        .animate-fade-in {
          animation: fade-in 1s ease-out;
        }

        @media (prefers-reduced-motion: reduce) {
          .animate-gradient-x,
          .animate-fade-in,
          .animate-pulse,
          .animate-bounce,
          .animate-spin,
          .animate-ping {
            animation: none !important;
          }
        }

        @media (max-width: 768px) {
          .floating-stats {
            display: none;
          }
        }

        @keyframes gridPulse {
          0%,
          100% {
            opacity: 0.3;
            transform: scale(1);
          }
          50% {
            opacity: 0.6;
            transform: scale(1.005);
          }
        }

        @keyframes gridFloat {
          0%,
          100% {
            opacity: 0.2;
            background-position: 0 0, 0 0;
          }
          50% {
            opacity: 0.5;
            background-position: 20px 20px, 20px 20px;
          }
        }

        @keyframes gridGlow {
          0%,
          100% {
            opacity: 0.25;
            filter: brightness(1);
          }
          50% {
            opacity: 0.45;
            filter: brightness(1.2);
          }
        }
      `}</style>
    </>
  );
}

export const getStaticProps: GetStaticProps = async ({ locale }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? "it", [
        "common",
        "polardrive",
      ])),
    },
  };
};
