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
import Link from "next/link";
import Image from "next/image";
import { ArrowRight } from "lucide-react";

// Register GSAP plugins
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function Products() {
  const { t } = useTranslation();
  const router = useRouter();
  const [mounted, setMounted] = useState(false);
  const sectionRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const productCardRef = useRef<HTMLDivElement>(null);
  const featuresRef = useRef<HTMLDivElement>(null);
  const particlesRef = useRef<HTMLDivElement>(null);

  const product = {
    id: "polardrive",
    name: "PolarDrive™",
    tagline: "La scelta naturale per il futuro digitale sostenibile",
    description:
      "Sistema avanzato di raccolta e analisi dati basato su Intelligenza Artificiale proprietaria. Elaborazione locale, privacy garantita, risultati intelligenti.",
    image: "/logo/DataPolar_Logo_PolarDrive.png",
    href: "/products/polardrive",
    highlights: [
      "🌍 AI eseguita in locale su server europei",
      "🔒 100% GDPR Compliant",
      "⚡ Processing real-time",
      "🎯 Analisi predittive avanzate",
    ],
  };

  useEffect(() => {
    setMounted(true);
    logFrontendEvent(
      "ProductsPage",
      "INFO",
      "Products page loaded",
      `Lang: ${router.locale}, URL: ${window.location.href}`
    );

    // Create particle system
    createParticleSystem();

    return () => {
      ScrollTrigger.getAll().forEach((trigger) => trigger.kill());
    };
  }, [router.locale]);

  useEffect(() => {
    if (
      mounted &&
      titleRef.current &&
      productCardRef.current &&
      featuresRef.current
    ) {
      // Entrance animations
      const tl = gsap.timeline();

      // Set initial states
      gsap.set([titleRef.current, productCardRef.current], {
        opacity: 0,
        y: 60,
      });

      // Title animation
      tl.to(titleRef.current, {
        opacity: 1,
        y: 0,
        duration: 0.8,
        ease: "power3.out",
      })
        // Product card animation
        .to(
          productCardRef.current,
          {
            opacity: 1,
            y: 0,
            duration: 0.8,
            ease: "back.out(1.2)",
          },
          "-=0.4"
        );

      // Features grid animation
      const featureCards =
        featuresRef.current.querySelectorAll(".feature-card");
      gsap.fromTo(
        featureCards,
        { opacity: 0, y: 30, scale: 0.9 },
        {
          opacity: 1,
          y: 0,
          scale: 1,
          duration: 0.6,
          ease: "back.out(1.2)",
          stagger: 0.1,
          delay: 0.6,
        }
      );
    }
  }, [mounted]);

  const createParticleSystem = () => {
    if (!particlesRef.current) return;

    const particleCount = 20;
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

  return (
    <>
      <Head>
        <title>{t("app.title.products")}</title>
        <meta
          name="description"
          content="Scopri i prodotti innovativi di DataPolar: soluzioni avanzate di AI e analisi dati per il futuro digitale sostenibile."
        />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <link rel="icon" href="/favicon.ico" />
      </Head>

      {/* Particle System Background */}
      <div
        ref={particlesRef}
        className="fixed inset-0 pointer-events-none z-0"
        style={{ zIndex: 1 }}
      />

      {/* Main Content */}
      <div className="relative z-10">
        <Header />

        {/* Single Main Section */}
        <section
          ref={sectionRef}
          className="relative w-full overflow-hidden py-24 px-6 pt-32 min-h-screen"
        >
          {/* Background */}
          <div className="absolute inset-0 z-0">
            {mounted && (
              <div className="absolute inset-0 bg-[length:60px_60px] bg-[radial-gradient(circle_at_1px_1px,rgba(167,198,237,0.1)_1px,transparent_0)]" />
            )}
            <div className="absolute inset-0 bg-gradient-to-br from-coldIndigo/20 via-glacierBlue/10 to-transparent" />
            <div className="absolute top-1/4 left-1/3 w-[500px] h-[500px] bg-gradient-radial from-coldIndigo/15 to-transparent rounded-full blur-3xl animate-pulse" />
            <div
              className="absolute bottom-1/3 right-1/4 w-[600px] h-[600px] bg-gradient-radial from-glacierBlue/15 to-transparent rounded-full blur-3xl animate-pulse"
              style={{ animationDelay: "2s" }}
            />
          </div>

          <div className="relative z-20 max-w-7xl mx-auto">
            {/* Page Title */}
            <div ref={titleRef} className="text-center mb-16">
              <h1 className="text-4xl md:text-6xl lg:text-7xl font-bold mb-6 leading-tight">
                <span className="text-polarNight/70 dark:text-articWhite/70">
                  I Nostri{" "}
                </span>
                <span className="bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-clip-text text-transparent">
                  Prodotti
                </span>
              </h1>

              <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80 max-w-3xl mx-auto">
                Soluzioni innovative di Intelligenza Artificiale e analisi dati
                progettate per trasformare il modo in cui le aziende gestiscono
                e utilizzano le informazioni. Privacy garantita.
              </p>
            </div>

            {/* PolarDrive Product Card */}
            <div ref={productCardRef} className="mb-12">
              <div className="bg-white/5 dark:bg-white/5 backdrop-blur-xl rounded-3xl p-8 md:p-12 border border-white/10 shadow-2xl">
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
                  {/* Product Info */}
                  <div className="space-y-6">
                    <div>
                      <h2 className="text-3xl md:text-5xl font-bold bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent mb-3">
                        {product.name}
                      </h2>
                      <p className="text-lg md:text-xl text-coldIndigo dark:text-glacierBlue font-semibold mb-4">
                        {product.tagline}
                      </p>
                      <p className="text-lg leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                        {product.description}
                      </p>
                    </div>

                    {/* Highlights */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      {product.highlights.map((highlight, idx) => (
                        <div
                          key={idx}
                          className="flex items-center space-x-2 text-sm text-polarNight/70 dark:text-articWhite/70"
                        >
                          <span>{highlight}</span>
                        </div>
                      ))}
                    </div>

                    {/* CTA */}
                    <Link
                      href={product.href}
                      className="inline-flex items-center gap-3 px-8 py-4 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-xl hover:shadow-coldIndigo/30 group"
                    >
                      <span>Scopri {product.name}</span>
                      <ArrowRight className="w-5 h-5 transition-transform duration-300 group-hover:translate-x-1" />
                    </Link>
                  </div>

                  {/* Product Image */}
                  <div className="relative">
                    <div className="relative w-full h-80 lg:h-96 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-2xl flex items-center justify-center backdrop-blur-sm border border-white/10">
                      <div className="relative w-full h-full p-8">
                        <Image
                          src="/logo/PolarDrive_Logo.svg"
                          alt="DataPolar Lettering"
                          fill
                          className="object-contain"
                          priority
                        />
                      </div>

                      {/* Floating elements around logo */}
                      <div className="absolute top-6 left-6 w-4 h-4 rounded-full bg-coldIndigo/40 animate-pulse" />
                      <div
                        className="absolute top-12 right-8 w-3 h-3 rounded-full bg-glacierBlue/40 animate-pulse"
                        style={{ animationDelay: "1s" }}
                      />
                      <div
                        className="absolute bottom-8 left-12 w-5 h-5 rounded-full bg-coldIndigo/30 animate-pulse"
                        style={{ animationDelay: "2s" }}
                      />
                      <div
                        className="absolute bottom-6 right-6 w-4 h-4 rounded-full bg-glacierBlue/30 animate-pulse"
                        style={{ animationDelay: "0.5s" }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>

      {/* Global Styles for Animations */}
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

        /* Improve text rendering */
        * {
          -webkit-font-smoothing: antialiased;
          -moz-osx-font-smoothing: grayscale;
        }

        /* Custom focus styles */
        button:focus-visible,
        a:focus-visible {
          outline: 2px solid #5c4de1;
          outline-offset: 2px;
        }

        /* Gradient background utilities */
        .bg-gradient-radial {
          background: radial-gradient(circle, var(--tw-gradient-stops));
        }

        /* Responsive adjustments */
        @media (max-width: 768px) {
          .particle {
            display: none;
          }
        }

        /* Prefers-reduced-motion */
        @media (prefers-reduced-motion: reduce) {
          .particle,
          .animate-pulse {
            animation: none !important;
          }
        }
      `}</style>
    </>
  );
}

export const getStaticProps: GetStaticProps = async ({ locale }) => {
  return {
    props: {
      ...(await serverSideTranslations(locale ?? "it", ["common"])),
    },
  };
};
