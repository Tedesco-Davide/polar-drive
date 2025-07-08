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
  const svgRef = useRef<SVGSVGElement>(null);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && quoteRef.current && titleRef.current && ctaRef.current) {
      // Animazione di entrata
      const tl = gsap.timeline();

      gsap.set([quoteRef.current, titleRef.current, ctaRef.current], {
        opacity: 0,
        y: 30,
      });

      tl.to(quoteRef.current, {
        opacity: 1,
        y: 0,
        duration: 0.8,
        ease: "power2.out",
      })
        .to(
          titleRef.current,
          {
            opacity: 1,
            y: 0,
            duration: 0.6,
            ease: "power2.out",
          },
          "-=0.4"
        )
        .to(
          ctaRef.current,
          {
            opacity: 1,
            y: 0,
            duration: 0.6,
            ease: "power2.out",
          },
          "-=0.3"
        );

      // Animazioni SVG
      if (svgRef.current) {
        // Animazione dei cerchi
        const circles = svgRef.current.querySelectorAll(".animated-circle");
        circles.forEach((circle, index) => {
          gsap.to(circle, {
            scale: 1.2,
            opacity: 0.8,
            duration: 3 + index * 0.5,
            repeat: -1,
            yoyo: true,
            ease: "sine.inOut",
            transformOrigin: "center",
          });
        });

        // Animazione delle linee
        const paths = svgRef.current.querySelectorAll(".data-path");
        paths.forEach((path, index) => {
          const length = (path as SVGPathElement).getTotalLength();
          gsap.set(path, {
            strokeDasharray: length,
            strokeDashoffset: length,
          });
          gsap.to(path, {
            strokeDashoffset: 0,
            duration: 2 + index * 0.5,
            ease: "power2.inOut",
            repeat: -1,
            repeatDelay: 3,
          });
        });

        // Animazione delle particelle
        const particles = svgRef.current.querySelectorAll(".particle");
        particles.forEach((particle, index) => {
          gsap.to(particle, {
            x: "random(-100, 100)",
            y: "random(-100, 100)",
            duration: "random(5, 10)",
            repeat: -1,
            yoyo: true,
            ease: "none",
            delay: index * 0.2,
          });
        });
      }

      // Animazione leggera per i pallini fluttuanti
      const floatingElements =
        heroRef.current?.querySelectorAll(".floating-element");
      floatingElements?.forEach((element, index) => {
        gsap.to(element, {
          y: index % 2 === 0 ? -20 : 20,
          x: index % 2 === 0 ? 10 : -10,
          duration: 4 + index * 0.5,
          repeat: -1,
          yoyo: true,
          ease: "sine.inOut",
          delay: index * 0.8,
        });
      });
    }
  }, [mounted]);

  const handleCtaClick = () => {
    if (ctaRef.current) {
      gsap.to(ctaRef.current, {
        scale: 0.98,
        duration: 0.1,
        yoyo: true,
        repeat: 1,
        ease: "power2.inOut",
      });
    }
    router.push("/products/polardrive");
  };

  return (
    <section
      ref={heroRef}
      className="relative w-full overflow-hidden min-h-screen flex items-center bg-gradient-to-br from-[#0f0f23] via-[#1a1a3e] to-[#2d2d5f]"
    >
      {/* SVG Background Animato */}
      <svg
        ref={svgRef}
        className="absolute inset-0 w-full h-full z-0"
        viewBox="0 0 1920 1080"
        preserveAspectRatio="xMidYMid slice"
      >
        <defs>
          <linearGradient id="gradient1" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#64b5f6" stopOpacity="0.15" />
            <stop offset="50%" stopColor="#42a5f5" stopOpacity="0.25" />
            <stop offset="100%" stopColor="#1e88e5" stopOpacity="0.35" />
          </linearGradient>
          <linearGradient id="gradient2" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#42a5f5" stopOpacity="0.3" />
            <stop offset="50%" stopColor="#1976d2" stopOpacity="0.2" />
            <stop offset="100%" stopColor="#0d47a1" stopOpacity="0.15" />
          </linearGradient>
          <filter id="glow">
            <feGaussianBlur stdDeviation="3" result="coloredBlur" />
            <feMerge>
              <feMergeNode in="coloredBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/* Griglia di sfondo */}
        <g opacity="0.15">
          <defs>
            <pattern
              id="grid"
              width="50"
              height="50"
              patternUnits="userSpaceOnUse"
            >
              <path
                d="M 50 0 L 0 0 0 50"
                fill="none"
                stroke="#64b5f6"
                strokeWidth="1"
                opacity="0.8"
              />
              <circle cx="25" cy="25" r="1" fill="#42a5f5" opacity="0.5" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#grid)" />
        </g>

        {/* Onde di energia */}
        <g className="energy-waves">
          <ellipse
            cx="960"
            cy="540"
            rx="200"
            ry="100"
            fill="none"
            stroke="url(#gradient1)"
            strokeWidth="2"
            opacity="0.4"
            className="animated-circle"
          />
          <ellipse
            cx="960"
            cy="540"
            rx="150"
            ry="75"
            fill="none"
            stroke="url(#gradient2)"
            strokeWidth="2"
            opacity="0.3"
            className="animated-circle"
          />
        </g>

        {/* Forme geometriche animate */}
        <g className="animated-shapes">
          <circle
            cx="200"
            cy="200"
            r="60"
            fill="url(#gradient1)"
            opacity="0.4"
            filter="url(#glow)"
            className="animated-circle"
          />
          <circle
            cx="1600"
            cy="200"
            r="40"
            fill="url(#gradient2)"
            opacity="0.3"
            filter="url(#glow)"
            className="animated-circle"
          />
          <polygon
            points="1600,300 1650,200 1700,300 1650,400"
            fill="url(#gradient2)"
            opacity="0.3"
            filter="url(#glow)"
          />
          <rect
            x="100"
            y="600"
            width="80"
            height="80"
            fill="url(#gradient1)"
            opacity="0.3"
            rx="10"
          />
        </g>

        {/* Linee connesse */}
        <g className="data-connections">
          <path
            d="M200,200 Q960,100 1600,300"
            stroke="url(#gradient1)"
            strokeWidth="3"
            fill="none"
            opacity="0.4"
            filter="url(#glow)"
            className="data-path"
          />
          <path
            d="M300,800 Q800,600 1400,800"
            stroke="url(#gradient2)"
            strokeWidth="2"
            fill="none"
            opacity="0.3"
            className="data-path"
          />
          <path
            d="M100,600 Q960,300 1500,700"
            stroke="url(#gradient1)"
            strokeWidth="2"
            fill="none"
            opacity="0.25"
            className="data-path"
          />
        </g>

        {/* Particelle */}
        <g className="particles">
          <circle
            cx="100"
            cy="100"
            r="3"
            fill="#64b5f6"
            opacity="0.8"
            filter="url(#glow)"
            className="particle"
          />
          <circle
            cx="1800"
            cy="100"
            r="4"
            fill="#42a5f5"
            opacity="0.7"
            filter="url(#glow)"
            className="particle"
          />
          <circle
            cx="500"
            cy="1000"
            r="2"
            fill="#1e88e5"
            opacity="0.6"
            className="particle"
          />
          <circle
            cx="1500"
            cy="900"
            r="3"
            fill="#4fc3f7"
            opacity="0.5"
            className="particle"
          />
        </g>

        {/* Nodi di rete */}
        <g className="network-nodes">
          <circle cx="300" cy="300" r="8" fill="#64b5f6" opacity="0.4">
            <animate
              attributeName="opacity"
              values="0.2;0.8;0.2"
              dur="3s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="r"
              values="8;12;8"
              dur="4s"
              repeatCount="indefinite"
            />
          </circle>
          <circle cx="1200" cy="400" r="6" fill="#42a5f5" opacity="0.3">
            <animate
              attributeName="opacity"
              values="0.1;0.6;0.1"
              dur="4s"
              repeatCount="indefinite"
            />
          </circle>
          <circle cx="700" cy="700" r="10" fill="#1e88e5" opacity="0.5">
            <animate
              attributeName="opacity"
              values="0.3;0.9;0.3"
              dur="2s"
              repeatCount="indefinite"
            />
          </circle>
        </g>
      </svg>

      {/* Pallini fluttuanti */}
      <div className="absolute inset-0 z-10 pointer-events-none">
        <div className="floating-element absolute top-[20%] left-[10%] w-2 h-2 rounded-full bg-[#64b5f6] opacity-60" />
        <div className="floating-element absolute top-[60%] right-[20%] w-2 h-2 rounded-full bg-[#42a5f5] opacity-60" />
        <div className="floating-element absolute bottom-[20%] left-[30%] w-2 h-2 rounded-full bg-[#1e88e5] opacity-60" />
        <div className="floating-element absolute top-[30%] right-[30%] w-2 h-2 rounded-full bg-[#4fc3f7] opacity-60" />
        <div className="floating-element absolute bottom-[50%] left-[50%] w-2 h-2 rounded-full bg-[#81c784] opacity-60" />
      </div>

      {/* Content */}
      <div className="relative z-20 container mx-auto px-6 text-center lg:mt-24">
        {/* Citazione in evidenza */}
        <div className="text-center mb-8">
          <blockquote className="text-2xl md:text-4xl font-light italic text-[#b0c4de] mb-6">
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

        @keyframes float-vertical {
          0%,
          100% {
            transform: translateY(0px);
          }
          50% {
            transform: translateY(-20px);
          }
        }

        @keyframes pulse-glow {
          0%,
          100% {
            opacity: 0.4;
          }
          50% {
            opacity: 0.8;
          }
        }

        /* Prefers-reduced-motion per accessibilità */
        @media (prefers-reduced-motion: reduce) {
          .floating-element,
          .animated-circle,
          .data-path,
          .particle {
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
