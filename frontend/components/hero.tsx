"use client";

import { useEffect, useState, useRef } from "react";
import { useTranslation } from "next-i18next";
import { gsap } from "gsap";

export default function Hero() {
  const { t } = useTranslation();
  const [mounted, setMounted] = useState(false);
  const heroRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const subtitleRef = useRef<HTMLParagraphElement>(null);
  const ctaRef = useRef<HTMLDivElement>(null);
  const orbitRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && titleRef.current && subtitleRef.current && ctaRef.current) {
      // Hero entrance animation più dinamica
      const tl = gsap.timeline();

      // Set initial states
      gsap.set([titleRef.current, subtitleRef.current, ctaRef.current], {
        opacity: 0,
        y: 80,
        scale: 0.8,
      });

      // Animate in sequence con bounce
      tl.to(titleRef.current, {
        opacity: 1,
        y: 0,
        scale: 1,
        duration: 1.2,
        ease: "back.out(1.7)",
      })
        .to(
          subtitleRef.current,
          {
            opacity: 1,
            y: 0,
            scale: 1,
            duration: 0.8,
            ease: "back.out(1.4)",
          },
          "-=0.6"
        )
        .to(
          ctaRef.current,
          {
            opacity: 1,
            y: 0,
            scale: 1,
            duration: 0.6,
            ease: "back.out(1.2)",
          },
          "-=0.4"
        );

      // Floating elements animation più vicini al centro
      const floatingElements =
        heroRef.current?.querySelectorAll(".floating-element");
      floatingElements?.forEach((element, index) => {
        gsap.to(element, {
          y: "random(-30, 30)",
          x: "random(-40, 40)",
          rotation: "random(-360, 360)",
          duration: "random(3, 6)",
          repeat: -1,
          yoyo: true,
          ease: "sine.inOut",
          delay: index * 0.3,
        });
      });

      // Orbital elements animation
      const orbitalElements =
        heroRef.current?.querySelectorAll(".orbital-element");
      orbitalElements?.forEach((element, index) => {
        gsap.to(element, {
          rotation: 360,
          duration: 20 + index * 5,
          repeat: -1,
          ease: "none",
        });
      });

      // Pulsing background effect
      gsap.to(".pulse-bg", {
        scale: 1.1,
        opacity: 0.8,
        duration: 4,
        repeat: -1,
        yoyo: true,
        ease: "sine.inOut",
      });

      // Continuous title glow
      gsap.to(titleRef.current, {
        textShadow: "0 0 30px rgba(92, 77, 225, 0.5)",
        duration: 3,
        repeat: -1,
        yoyo: true,
        ease: "sine.inOut",
      });
    }
  }, [mounted]);

  const handleCtaClick = () => {
    // Animazione click del CTA
    if (ctaRef.current) {
      gsap.to(ctaRef.current, {
        scale: 0.95,
        duration: 0.1,
        yoyo: true,
        repeat: 1,
        ease: "power2.inOut",
      });
    }

    document.getElementById("contacts")?.scrollIntoView({ behavior: "smooth" });
  };

  return (
    <section
      ref={heroRef}
      className="relative w-full overflow-hidden min-h-screen flex items-center pt-16"
    >
      {/* Enhanced Background with multiple layers */}
      <div className="absolute inset-0 z-0">
        {mounted && (
          <>
            {/* Primary grid */}
            <div className="hero-grid absolute inset-0 bg-[length:60px_60px] bg-[radial-gradient(circle_at_1px_1px,rgba(255,255,255,0.15)_1px,transparent_0)]" />

            {/* Secondary smaller grid */}
            <div className="hero-grid-small absolute inset-0 bg-[length:20px_20px] bg-[radial-gradient(circle_at_0.5px_0.5px,rgba(167,198,237,0.2)_0.5px,transparent_0)]" />
          </>
        )}
      </div>

      {/* Multiple Gradient Overlays */}
      <div className="absolute inset-0 z-5">
        <div className="pulse-bg absolute inset-0 bg-gradient-radial from-coldIndigo/30 via-glacierBlue/20 to-transparent" />
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-gradient-radial from-coldIndigo/20 to-transparent rounded-full blur-3xl animate-pulse" />
        <div
          className="absolute bottom-1/3 right-1/3 w-80 h-80 bg-gradient-radial from-glacierBlue/25 to-transparent rounded-full blur-2xl animate-pulse"
          style={{ animationDelay: "2s" }}
        />
      </div>

      {/* Orbital System - elementi che ruotano attorno al centro */}
      <div ref={orbitRef} className="absolute inset-0 z-10 pointer-events-none">
        {/* Orbita interna */}
        <div className="orbital-element absolute top-1/2 left-1/2 w-80 h-80 -translate-x-1/2 -translate-y-1/2">
          <div className="relative w-full h-full">
            <div className="absolute top-0 left-1/2 w-4 h-4 bg-gradient-to-r from-coldIndigo to-glacierBlue rounded-full -translate-x-1/2 shadow-lg animate-pulse" />
            <div
              className="absolute bottom-0 left-1/2 w-6 h-6 bg-gradient-to-r from-glacierBlue to-coldIndigo rounded-full -translate-x-1/2 shadow-lg animate-pulse"
              style={{ animationDelay: "1s" }}
            />
            <div
              className="absolute left-0 top-1/2 w-3 h-3 bg-gradient-to-r from-coldIndigo/70 to-glacierBlue/70 rounded-full -translate-y-1/2 shadow-lg animate-pulse"
              style={{ animationDelay: "2s" }}
            />
            <div
              className="absolute right-0 top-1/2 w-5 h-5 bg-gradient-to-r from-glacierBlue/80 to-coldIndigo/80 rounded-full -translate-y-1/2 shadow-lg animate-pulse"
              style={{ animationDelay: "3s" }}
            />
          </div>
        </div>

        {/* Orbita media */}
        <div className="orbital-element absolute top-1/2 left-1/2 w-[500px] h-[500px] -translate-x-1/2 -translate-y-1/2">
          <div className="relative w-full h-full">
            <div className="absolute top-16 right-16 w-8 h-8 bg-gradient-to-br from-coldIndigo/60 to-glacierBlue/60 rounded-full backdrop-blur-sm border border-white/20 shadow-xl" />
            <div className="absolute bottom-20 left-16 w-6 h-6 bg-gradient-to-br from-glacierBlue/70 to-coldIndigo/70 rounded-full backdrop-blur-sm border border-white/30 shadow-xl" />
            <div className="absolute top-32 left-20 w-4 h-4 bg-gradient-to-br from-coldIndigo/50 to-glacierBlue/50 rounded-full backdrop-blur-sm border border-white/10 shadow-lg" />
          </div>
        </div>

        {/* Orbita esterna */}
        <div className="orbital-element absolute top-1/2 left-1/2 w-[700px] h-[700px] -translate-x-1/2 -translate-y-1/2">
          <div className="relative w-full h-full">
            <div className="absolute top-8 left-1/3 w-3 h-3 bg-gradient-to-br from-glacierBlue/40 to-coldIndigo/40 rounded-full backdrop-blur-sm" />
            <div className="absolute bottom-12 right-1/4 w-7 h-7 bg-gradient-to-br from-coldIndigo/40 to-glacierBlue/40 rounded-full backdrop-blur-sm border border-white/20" />
          </div>
        </div>
      </div>

      {/* Floating Elements più vicini al centro */}
      <div className="absolute inset-0 z-15 pointer-events-none">
        <div className="floating-element absolute top-1/3 left-1/2 -translate-x-16 w-12 h-12 rounded-full bg-gradient-to-br from-coldIndigo/30 to-glacierBlue/30 backdrop-blur-md border border-white/20 shadow-2xl" />
        <div className="floating-element absolute top-1/2 right-1/2 translate-x-20 w-16 h-16 rounded-full bg-gradient-to-br from-glacierBlue/25 to-coldIndigo/25 backdrop-blur-md border border-white/15 shadow-2xl" />
        <div className="floating-element absolute bottom-1/3 left-1/2 translate-x-8 w-10 h-10 rounded-full bg-gradient-to-br from-coldIndigo/35 to-glacierBlue/35 backdrop-blur-md border border-white/25 shadow-2xl" />
        <div className="floating-element absolute top-2/3 left-1/3 w-14 h-14 rounded-full bg-gradient-to-br from-glacierBlue/20 to-coldIndigo/20 backdrop-blur-md border border-white/10 shadow-2xl" />
        <div className="floating-element absolute top-1/4 right-1/3 w-8 h-8 rounded-full bg-gradient-to-br from-coldIndigo/40 to-glacierBlue/40 backdrop-blur-md border border-white/30 shadow-2xl" />
      </div>

      {/* Content with enhanced animations */}
      <div className="relative z-20 container mx-auto px-6 text-center">
        <h1
          ref={titleRef}
          className="text-4xl md:text-6xl lg:text-7xl font-bold mb-6 leading-tight"
        >
          <span className="text-polarNight dark:text-articWhite inline-block animate-pulse">
            Innovazione
          </span>
          <br />
          <span className="bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-clip-text text-transparent bg-[length:200%_100%] animate-gradient-shift">
            DataPolar
          </span>
        </h1>

        <p
          ref={subtitleRef}
          className="text-lg md:text-xl text-polarNight/80 dark:text-articWhite/80 max-w-2xl mx-auto mb-8 leading-relaxed"
        >
          {t("hero.subtitle")}
        </p>

        <div
          ref={ctaRef}
          onClick={handleCtaClick}
          className="inline-flex items-center gap-3 px-10 py-5 bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-[length:200%_100%] text-white font-semibold rounded-full transition-all duration-300 hover:scale-110 hover:shadow-2xl hover:shadow-coldIndigo/40 cursor-pointer group relative overflow-hidden animate-gradient-shift"
        >
          {/* Shine effect interno */}
          <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/30 to-transparent -skew-x-12 -translate-x-full group-hover:translate-x-full transition-transform duration-700" />

          <span className="relative z-10">{t("hero.CTA")}</span>
          <span className="relative z-10 transition-transform duration-300 group-hover:translate-x-2 group-hover:scale-125">
            →
          </span>
        </div>
      </div>

      <style jsx>{`
        @keyframes gradient-shift {
          0% {
            background-position: 0% 50%;
          }
          50% {
            background-position: 100% 50%;
          }
          100% {
            background-position: 0% 50%;
          }
        }

        .animate-gradient-shift {
          animation: gradient-shift 4s ease infinite;
        }

        .hero-grid {
          animation: grid-float 15s ease-in-out infinite;
        }

        .hero-grid-small {
          animation: grid-float 25s ease-in-out infinite reverse;
        }

        @keyframes grid-float {
          0%,
          100% {
            transform: translateY(0) translateX(0);
          }
          25% {
            transform: translateY(-8px) translateX(4px);
          }
          50% {
            transform: translateY(-15px) translateX(0);
          }
          75% {
            transform: translateY(-8px) translateX(-4px);
          }
        }

        .floating-element {
          animation: float-enhanced 4s ease-in-out infinite;
        }

        .floating-element:nth-child(1) {
          animation-delay: 0s;
          animation-duration: 5s;
        }

        .floating-element:nth-child(2) {
          animation-delay: 1s;
          animation-duration: 6s;
        }

        .floating-element:nth-child(3) {
          animation-delay: 2s;
          animation-duration: 4.5s;
        }

        .floating-element:nth-child(4) {
          animation-delay: 0.5s;
          animation-duration: 5.5s;
        }

        .floating-element:nth-child(5) {
          animation-delay: 1.5s;
          animation-duration: 4s;
        }

        @keyframes float-enhanced {
          0%,
          100% {
            transform: translateY(0) rotate(0deg) scale(1);
          }
          25% {
            transform: translateY(-15px) rotate(90deg) scale(1.1);
          }
          50% {
            transform: translateY(-25px) rotate(180deg) scale(0.9);
          }
          75% {
            transform: translateY(-15px) rotate(270deg) scale(1.05);
          }
        }

        /* Responsive adjustments */
        @media (max-width: 768px) {
          .orbital-element {
            transform: scale(0.7) translate(-50%, -50%);
          }

          .floating-element {
            animation-duration: 3s;
          }
        }
      `}</style>
    </section>
  );
}
