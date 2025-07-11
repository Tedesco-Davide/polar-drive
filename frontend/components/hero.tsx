"use client";

import { useTranslation } from "next-i18next";
import { useEffect, useState, useRef } from "react";
import router from "next/router";

export default function Hero() {
  const { t } = useTranslation("hero");
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
      // Animazione di entrata con un approccio diverso
      const elements = [quoteRef.current, titleRef.current, ctaRef.current];

      elements.forEach((element, index) => {
        element.style.opacity = "0";
        element.style.transform = "translateY(30px)";

        setTimeout(() => {
          element.style.transition =
            "all 0.8s cubic-bezier(0.25, 0.46, 0.45, 0.94)";
          element.style.opacity = "1";
          element.style.transform = "translateY(0)";
        }, index * 200);
      });
    }
  }, [mounted]);

  const handleCtaClick = () => {
    if (ctaRef.current) {
      ctaRef.current.style.transform = "scale(0.98)";
      setTimeout(() => {
        if (ctaRef.current) {
          ctaRef.current.style.transform = "scale(1)";
        }
      }, 100);
    }
    router.push("/polardrive");
  };

  return (
    <section
      ref={heroRef}
      className="relative w-full overflow-hidden min-h-screen flex items-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-950 dark:via-blue-950 dark:to-indigo-900"
    >
      {/* SVG Background */}
      <svg
        ref={svgRef}
        className="absolute inset-0 w-full h-full z-0"
        viewBox="0 0 1920 1080"
        preserveAspectRatio="xMidYMid slice"
      >
        <defs>
          {/* Gradienti più moderni */}
          <linearGradient id="dataFlow" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#3b82f6" stopOpacity="0.4" />
            <stop offset="50%" stopColor="#8b5cf6" stopOpacity="0.6" />
            <stop offset="100%" stopColor="#06b6d4" stopOpacity="0.3" />
          </linearGradient>

          <radialGradient id="neuralGlow" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#10b981" stopOpacity="0.8" />
            <stop offset="50%" stopColor="#3b82f6" stopOpacity="0.4" />
            <stop offset="100%" stopColor="#8b5cf6" stopOpacity="0.1" />
          </radialGradient>

          <linearGradient id="quantumGrad" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#f59e0b" stopOpacity="0.6" />
            <stop offset="50%" stopColor="#ef4444" stopOpacity="0.4" />
            <stop offset="100%" stopColor="#8b5cf6" stopOpacity="0.8" />
          </linearGradient>

          {/* Filtri avanzati */}
          <filter id="neuralBlur">
            <feGaussianBlur stdDeviation="4" result="coloredBlur" />
            <feMerge>
              <feMergeNode in="coloredBlur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>

        {/* Griglia esagonale di sfondo */}
        <rect width="100%" height="100%" fill="url(#hexGrid)" />

        {/* Rete neurale centrale */}
        <g className="neural-network">
          {/* Nodi principali */}
          <circle
            cx="960"
            cy="300"
            r="12"
            fill="url(#neuralGlow)"
            filter="url(#neuralBlur)"
          >
            <animate
              attributeName="r"
              values="12;18;12"
              dur="3s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.6;1;0.6"
              dur="2s"
              repeatCount="indefinite"
            />
          </circle>

          <circle
            cx="700"
            cy="450"
            r="8"
            fill="url(#neuralGlow)"
            filter="url(#neuralBlur)"
          >
            <animate
              attributeName="r"
              values="8;14;8"
              dur="2.5s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.4;0.9;0.4"
              dur="3s"
              repeatCount="indefinite"
            />
          </circle>

          <circle
            cx="1220"
            cy="450"
            r="10"
            fill="url(#neuralGlow)"
            filter="url(#neuralBlur)"
          >
            <animate
              attributeName="r"
              values="10;16;10"
              dur="4s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.5;1;0.5"
              dur="2.5s"
              repeatCount="indefinite"
            />
          </circle>

          <circle
            cx="960"
            cy="600"
            r="14"
            fill="url(#neuralGlow)"
            filter="url(#neuralBlur)"
          >
            <animate
              attributeName="r"
              values="14;20;14"
              dur="3.5s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.7;1;0.7"
              dur="3s"
              repeatCount="indefinite"
            />
          </circle>

          {/* Connessioni dinamiche */}
          <g stroke="url(#dataFlow)" strokeWidth="2" fill="none" opacity="0.6">
            <path d="M960,300 L700,450">
              <animate
                attributeName="stroke-dasharray"
                values="0,1000;1000,0;0,1000"
                dur="4s"
                repeatCount="indefinite"
              />
            </path>
            <path d="M960,300 L1220,450">
              <animate
                attributeName="stroke-dasharray"
                values="0,1000;1000,0;0,1000"
                dur="3s"
                repeatCount="indefinite"
              />
            </path>
            <path d="M700,450 L960,600">
              <animate
                attributeName="stroke-dasharray"
                values="0,1000;1000,0;0,1000"
                dur="5s"
                repeatCount="indefinite"
              />
            </path>
            <path d="M1220,450 L960,600">
              <animate
                attributeName="stroke-dasharray"
                values="0,1000;1000,0;0,1000"
                dur="3.5s"
                repeatCount="indefinite"
              />
            </path>
          </g>
        </g>

        {/* Strutture dati fluttuanti */}
        <g className="data-structures">
          {/* Cubi di dati */}
          <g transform="translate(280,280)">
            <rect
              width="60"
              height="60"
              fill="url(#quantumGrad)"
              opacity="0.4"
              rx="8"
            >
              <animateTransform
                attributeName="transform"
                type="rotate"
                values="0;360"
                dur="20s"
                repeatCount="indefinite"
              />
            </rect>
            <rect
              width="40"
              height="40"
              x="10"
              y="10"
              fill="none"
              stroke="#10b981"
              strokeWidth="2"
              opacity="0.6"
              rx="4"
            >
              <animateTransform
                attributeName="transform"
                type="rotate"
                values="360;0"
                dur="15s"
                repeatCount="indefinite"
              />
            </rect>
          </g>

          {/* Piramidi di algoritmi */}
          <g transform="translate(1600,250)">
            <polygon
              points="0,60 30,0 60,60"
              fill="url(#dataFlow)"
              opacity="0.5"
            >
              <animateTransform
                attributeName="transform"
                type="scale"
                values="1;1.2;1"
                dur="4s"
                repeatCount="indefinite"
              />
            </polygon>
            <polygon
              points="15,45 30,15 45,45"
              fill="none"
              stroke="#f59e0b"
              strokeWidth="2"
              opacity="0.8"
            />
          </g>

          {/* Spirali quantiche */}
          <g transform="translate(300,700)">
            <path
              d="M0,0 Q20,-20 40,0 Q60,20 80,0 Q100,-20 120,0"
              stroke="url(#neuralGlow)"
              strokeWidth="3"
              fill="none"
              opacity="0.6"
            >
              <animateTransform
                attributeName="transform"
                type="rotate"
                values="0;360"
                dur="12s"
                repeatCount="indefinite"
              />
            </path>
          </g>
        </g>

        {/* Particelle di dati intelligenti */}
        <g className="smart-particles">
          {Array.from({ length: 15 }).map((_, i) => (
            <circle
              key={i}
              cx={Math.random() * 1920}
              cy={Math.random() * 1080}
              r="3"
              fill={
                i % 3 === 0 ? "#10b981" : i % 3 === 1 ? "#3b82f6" : "#8b5cf6"
              }
              opacity="0.7"
            >
              <animate
                attributeName="cx"
                values={`${Math.random() * 1920};${Math.random() * 1920};${
                  Math.random() * 1920
                }`}
                dur={`${5 + Math.random() * 10}s`}
                repeatCount="indefinite"
              />
              <animate
                attributeName="cy"
                values={`${Math.random() * 1080};${Math.random() * 1080};${
                  Math.random() * 1080
                }`}
                dur={`${7 + Math.random() * 8}s`}
                repeatCount="indefinite"
              />
              <animate
                attributeName="opacity"
                values="0.2;0.9;0.2"
                dur={`${2 + Math.random() * 3}s`}
                repeatCount="indefinite"
              />
            </circle>
          ))}
        </g>

        {/* Onde di processamento AI */}
        <g className="ai-waves">
          <ellipse
            cx="960"
            cy="540"
            rx="300"
            ry="150"
            fill="none"
            stroke="url(#dataFlow)"
            strokeWidth="2"
            opacity="0.3"
          >
            <animate
              attributeName="rx"
              values="300;400;300"
              dur="6s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="ry"
              values="150;200;150"
              dur="6s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.1;0.5;0.1"
              dur="4s"
              repeatCount="indefinite"
            />
          </ellipse>

          <ellipse
            cx="960"
            cy="540"
            rx="200"
            ry="100"
            fill="none"
            stroke="url(#neuralGlow)"
            strokeWidth="3"
            opacity="0.4"
          >
            <animate
              attributeName="rx"
              values="200;300;200"
              dur="4s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="ry"
              values="100;150;100"
              dur="4s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.2;0.7;0.2"
              dur="3s"
              repeatCount="indefinite"
            />
          </ellipse>
        </g>

        {/* Codice binario fluttuante */}
        <g
          className="binary-code"
          fontSize="12"
          fontFamily="monospace"
          fill="#06b6d4"
          opacity="0.3"
        >
          <text x="100" y="100">
            01001000 01100101 01101100 01101100 01101111
          </text>
          <text x="1400" y="150">
            01000100 01100001 01110100 01100001
          </text>
          <text x="200" y="900">
            01010000 01101111 01101100 01100001 01110010
          </text>
          <text x="1500" y="950">
            01000001 01001001 00100000 01000100 01110010 01101001 01110110
            01100101
          </text>

          <animateTransform
            attributeName="transform"
            type="translate"
            values="0,0;0,-20;0,0"
            dur="8s"
            repeatCount="indefinite"
          />
        </g>

        {/* Matrice di trasformazione */}
        <g className="transformation-matrix">
          <rect
            x="1400"
            y="400"
            width="120"
            height="120"
            fill="none"
            stroke="url(#quantumGrad)"
            strokeWidth="2"
            opacity="0.4"
            rx="10"
          >
            <animate
              attributeName="stroke-width"
              values="2;4;2"
              dur="3s"
              repeatCount="indefinite"
            />
          </rect>
          <g stroke="url(#dataFlow)" strokeWidth="1" opacity="0.6">
            <line x1="1420" y1="420" x2="1500" y2="420" />
            <line x1="1420" y1="460" x2="1500" y2="460" />
            <line x1="1420" y1="500" x2="1500" y2="500" />
            <line x1="1440" y1="400" x2="1440" y2="480" />
            <line x1="1480" y1="400" x2="1480" y2="480" />
          </g>
        </g>
      </svg>

      {/* Elementi fluttuanti tecnologici */}
      <div className="absolute inset-0 z-10 pointer-events-none">
        {/* Icone tech animate */}
        <div className="absolute top-[25%] right-[12%] w-6 h-6 rounded-full bg-gradient-to-br from-green-400 to-blue-500 opacity-50 animate-bounce" />
        <div className="absolute bottom-[30%] left-[20%] w-10 h-10 rounded-xl bg-gradient-to-br from-orange-400 to-red-500 opacity-40 animate-pulse" />
        <div className="absolute top-[40%] right-[25%] w-4 h-4 rounded-full bg-gradient-to-br from-cyan-400 to-blue-500 opacity-10 animate-ping" />
        <div className="absolute bottom-[20%] right-[15%] w-6 h-6 rounded-lg bg-gradient-to-br from-purple-400 to-pink-500 opacity-50 animate-bounce" />

        {/* Elementi geometrici */}
        <div
          className="absolute top-[60%] left-[35%] w-3 h-3 rotate-45 bg-gradient-to-br from-yellow-400 to-orange-500 opacity-60 animate-spin"
          style={{ animationDuration: "8s" }}
        />
        <div className="absolute top-[85%] right-[30%] w-5 h-5 rotate-12 bg-gradient-to-br from-indigo-400 to-purple-500 opacity-45 animate-pulse" />
      </div>

      {/* Content con traduzioni */}
      <div className="relative z-20 container mx-auto px-6 text-center lg:mt-24">
        {/* Citazione in evidenza */}
        <div className="text-center mb-8">
          <blockquote className="text-2xl md:text-4xl font-light italic text-coldIndigo dark:text-[#b0c4de] mb-6">
            &quot;{t("hero.quote")}&quot;
          </blockquote>
        </div>

        {/* Titolo principale */}
        <h1
          ref={titleRef}
          className="text-4xl md:text-6xl lg:text-7xl font-bold mb-20 leading-tight"
        >
          <span className="text-slate-400">{t("hero.welcome")} </span>
          <span className="bg-gradient-to-r from-blue-400 via-cyan-400 to-blue-400 bg-clip-text text-transparent bg-[length:200%_100%] animate-gradient-shift">
            {t("hero.company_name")}
          </span>
        </h1>

        {/* CTA Button */}
        <div
          ref={ctaRef}
          onClick={handleCtaClick}
          className="mb-20 inline-flex items-center gap-3 px-10 py-5 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-xl hover:shadow-blue-500/30 cursor-pointer group"
        >
          <span>{t("hero.cta_button")}</span>
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

        @keyframes float {
          0%,
          100% {
            transform: translateY(0px);
          }
          50% {
            transform: translateY(-20px);
          }
        }

        @keyframes rotate-slow {
          from {
            transform: rotate(0deg);
          }
          to {
            transform: rotate(360deg);
          }
        }

        /* Responsive */
        @media (max-width: 768px) {
          .absolute.top-\\[15\\%\\] {
            display: none;
          }
          .absolute.bottom-\\[30\\%\\] {
            display: none;
          }
        }

        /* Accessibilità */
        @media (prefers-reduced-motion: reduce) {
          * {
            animation-duration: 0.01ms !important;
            animation-iteration-count: 1 !important;
            transition-duration: 0.01ms !important;
          }
        }
      `}</style>
    </section>
  );
}
