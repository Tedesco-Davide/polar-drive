import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import { useEffect, useRef } from "react";
import { useRouter } from "next/router";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/dist/ScrollTrigger";
import { logFrontendEvent } from "@/utils/logger";
import Head from "next/head";
import Header from "@/components/header";
import Hero from "@/components/hero";
import Mission from "@/components/mission";
import Contacts from "@/components/contacts";

// Register GSAP plugins
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function Home() {
  const { t } = useTranslation("header");
  const router = useRouter();
  const particlesRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    logFrontendEvent(
      "PublicLanding",
      "INFO",
      "Landing page loaded",
      `Lang: ${router.locale}, URL: ${window.location.href}`
    );

    // Create particle system
    createParticleSystem();

    // Initialize smooth scrolling
    initSmoothScrolling();

    // Cleanup function
    return () => {
      ScrollTrigger.getAll().forEach((trigger) => trigger.kill());
    };
  }, [router.locale]);

  const createParticleSystem = () => {
    if (!particlesRef.current) return;

    const particleCount = 30;
    const container = particlesRef.current;

    // Clear existing particles
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
        animation: particle-float ${Math.random() * 10 + 10}s linear infinite;
        animation-delay: ${Math.random() * 15}s;
      `;
      container.appendChild(particle);
    }
  };

  const initSmoothScrolling = () => {
    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
      anchor.addEventListener("click", (e: Event) => {
        e.preventDefault();
        const target = e.currentTarget as HTMLAnchorElement;
        const href = target.getAttribute("href");
        if (href) {
          const targetElement = document.querySelector(href);
          if (targetElement) {
            targetElement.scrollIntoView({
              behavior: "smooth",
              block: "start",
            });
          }
        }
      });
    });
  };

  return (
    <>
      <Head>
        <title>{t("meta.title")}</title>
        <meta name="description" content={t("meta.description")} />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <link rel="icon" href="/favicon.ico" />

        {/* Preload GSAP for better performance */}
        <link
          rel="preload"
          href="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.2/gsap.min.js"
          as="script"
        />
        <link
          rel="preload"
          href="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.2/ScrollTrigger.min.js"
          as="script"
        />
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
        <main>
          <Hero />
          <Mission />
          <Contacts />
        </main>
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

        /* Ensure smooth transitions */
        * {
          -webkit-font-smoothing: antialiased;
          -moz-osx-font-smoothing: grayscale;
        }

        /* Custom focus styles */
        button:focus-visible,
        a:focus-visible,
        input:focus-visible,
        textarea:focus-visible {
          outline: 2px solid #5c4de1;
          outline-offset: 2px;
        }

        /* Improve text rendering */
        body {
          text-rendering: optimizeLegibility;
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
        "header",
        "hero",
        "mission",
        "contacts",
      ])),
    },
  };
};
