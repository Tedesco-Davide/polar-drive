"use client";

import { useEffect, useState, useRef } from "react";
import { useRouter } from "next/router";
import { gsap } from "gsap";

export default function Hero() {
  const router = useRouter();
  const [mounted, setMounted] = useState(false);
  const heroRef = useRef<HTMLElement>(null);
  const quoteRef = useRef<HTMLParagraphElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const ctaRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && quoteRef.current && titleRef.current && ctaRef.current) {
      // Animazione di entrata semplificata e performante
      const tl = gsap.timeline();

      // Set initial states
      gsap.set([quoteRef.current, titleRef.current], {
        opacity: 0,
        y: 30,
      });

      // Animate in sequence (senza il CTA button)
      tl.to(quoteRef.current, {
        opacity: 1,
        y: 0,
        duration: 0.8,
        ease: "power2.out",
      }).to(
        titleRef.current,
        {
          opacity: 1,
          y: 0,
          duration: 0.6,
          ease: "power2.out",
        },
        "-=0.4"
      );

      // Animazione leggera per i pallini fluttuanti (solo alcuni)
      const floatingElements =
        heroRef.current?.querySelectorAll(".floating-element");
      floatingElements?.forEach((element, index) => {
        // Solo animazione verticale leggera
        gsap.to(element, {
          y: index % 2 === 0 ? -15 : 15,
          duration: 3 + index * 0.5,
          repeat: -1,
          yoyo: true,
          ease: "sine.inOut",
          delay: index * 0.8,
        });
      });
    }
  }, [mounted]);

  const handleCtaClick = () => {
    // Animazione click leggera
    if (ctaRef.current) {
      gsap.to(ctaRef.current, {
        scale: 0.98,
        duration: 0.1,
        yoyo: true,
        repeat: 1,
        ease: "power2.inOut",
      });
    }

    // Naviga alla pagina PolarDrive
    router.push("/products/polardrive");
  };

  return (
    <section
      ref={heroRef}
      className="relative w-full overflow-hidden min-h-screen flex items-center pt-16"
    >
      {/* Background semplificato */}
      <div className="absolute inset-0 z-0">
        {mounted && (
          <div className="hero-grid absolute inset-0 bg-[length:60px_60px] bg-[radial-gradient(circle_at_1px_1px,rgba(255,255,255,0.1)_1px,transparent_0)]" />
        )}
      </div>

      {/* Gradient overlay semplificato */}
      <div className="absolute inset-0 z-5">
        <div className="absolute inset-0 bg-gradient-radial from-coldIndigo/20 via-glacierBlue/10 to-transparent" />
        <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-gradient-radial from-coldIndigo/15 to-transparent rounded-full blur-3xl" />
        <div className="absolute bottom-1/3 right-1/3 w-80 h-80 bg-gradient-radial from-glacierBlue/15 to-transparent rounded-full blur-2xl" />
      </div>

      {/* Pallini fluttuanti ridotti e ottimizzati */}
      <div className="absolute inset-0 z-10 pointer-events-none">
        <div className="floating-element absolute top-1/4 left-1/4 w-8 h-8 rounded-full bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 backdrop-blur-sm" />
        <div className="floating-element absolute top-1/3 right-1/3 w-12 h-12 rounded-full bg-gradient-to-br from-glacierBlue/15 to-coldIndigo/15 backdrop-blur-sm" />
        <div className="floating-element absolute bottom-1/4 left-1/3 w-6 h-6 rounded-full bg-gradient-to-br from-coldIndigo/25 to-glacierBlue/25 backdrop-blur-sm" />
        <div className="floating-element absolute bottom-1/3 right-1/4 w-10 h-10 rounded-full bg-gradient-to-br from-glacierBlue/20 to-coldIndigo/20 backdrop-blur-sm" />
      </div>

      {/* Content */}
      <div className="relative z-20 container mx-auto px-6 text-center lg:mt-24">
        {/* Citazione in evidenza */}
        <div className="text-center mb-8">
          <blockquote className="text-2xl md:text-4xl font-bold italic text-coldIndigo dark:text-glacierBlue mb-6">
            &quot;Immagina un mondo in cui ogni decisione aziendale sia guidata
            da dati intelligenti&quot;
          </blockquote>
        </div>

        {/* Titolo principale */}
        <h1
          ref={titleRef}
          className="text-4xl md:text-6xl lg:text-7xl font-bold mb-20 leading-tight"
        >
          <span className="text-polarNight/70 dark:text-articWhite/70">
            Benvenuto in{" "}
          </span>
          <span className="bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-clip-text text-transparent bg-[length:200%_100%] animate-gradient-shift">
            DataPolar
          </span>
        </h1>

        {/* CTA Button */}
        <div
          ref={ctaRef}
          onClick={handleCtaClick}
          className="mb-20 inline-flex items-center gap-3 px-10 py-5 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-xl hover:shadow-coldIndigo/30 cursor-pointer group"
        >
          <span>Scopri PolarDrive™</span>
        </div>
      </div>

      <style jsx>{`
        .hero-grid {
          animation: grid-float 20s ease-in-out infinite;
        }

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
          animation: gradient-shift 3s ease-in-out infinite;
        }

        @keyframes grid-float {
          0%,
          100% {
            transform: translateY(0);
          }
          50% {
            transform: translateY(-10px);
          }
        }

        /* Prefers-reduced-motion per accessibilità */
        @media (prefers-reduced-motion: reduce) {
          .hero-grid,
          .floating-element {
            animation: none !important;
          }
        }

        /* Responsive adjustments */
        @media (max-width: 768px) {
          .floating-element {
            display: none;
          }
        }
      `}</style>
    </section>
  );
}
