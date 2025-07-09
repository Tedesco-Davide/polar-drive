"use client";

// import { useTranslation } from "next-i18next";
import { useEffect, useRef } from "react";
import { useRouter } from "next/router";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/dist/ScrollTrigger";

// Register ScrollTrigger plugin
if (typeof window !== "undefined") {
  gsap.registerPlugin(ScrollTrigger);
}

export default function Mission() {
  // const { t } = useTranslation();
  const router = useRouter();
  const sectionRef = useRef<HTMLElement>(null);

  const navigateToPolarDrive = () => {
    router.push("/polardrive");
  };

  useEffect(() => {
    if (typeof window !== "undefined") {
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
              },
            }
          );
        });
    }
  }, []);

  return (
    <section
      ref={sectionRef}
      id="mission"
      className="relative w-full overflow-hidden py-12 px-6 scroll-mt-16 bg-gradient-to-bl from-slate-50 via-blue-50 to-indigo-100 dark:from-indigo-900 dark:via-blue-950 dark:to-indigo-950"
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

      {/* Background Gradient Enhanced - RIMOSSO (sostituito dal gradient nel className principale) */}

      {/* Floating Grid Pattern - RIMOSSO (sostituito dalla griglia esagonale SVG) */}

      <div className="relative z-20 max-w-7xl mx-auto">
        {/* Chi Siamo */}
        <div className="mb-20 animate-on-scroll">
          <h2 className="text-3xl md:text-5xl font-bold bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent text-center mb-8">
            Chi Siamo
          </h2>
          <div className="max-w-4xl mx-auto text-center space-y-6">
            <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80">
              <strong className="text-coldIndigo dark:text-glacierBlue">
                DataPolar
              </strong>{" "}
              è una Tech Data Company che trasforma informazioni complesse in
              soluzioni concrete. Specializziamo nella raccolta, elaborazione e
              analisi intelligente dei dati, creando valore tangibile per i
              nostri partner attraverso insights strategici e tecnologie
              all&apos;avanguardia
            </p>
            <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80">
              Grazie alle tecnologie di Intelligenza Artificiale più avanzate,
              DataPolar trasforma i dati in vantaggio competitivo per i
              professionisti di ogni settore. La nostra IA proprietaria{" "}
              <strong className="text-coldIndigo dark:text-glacierBlue">
                PolarAi™
              </strong>{" "}
              rappresenta il cuore tecnologico dell&apos;azienda
            </p>
          </div>
        </div>

        {/* Stats Section */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-8 mb-20">
          {[
            { number: 100, suffix: "%", label: "Privacy Compliance" },
            { number: 24, suffix: "/7", label: "Monitoraggio Dati" },
            { number: 0, suffix: "", label: "Data Leakage" },
            { number: 5, suffix: "★", label: "Rating Sicurezza" },
          ].map((stat, index) => (
            <div key={index} className="text-center card-stagger">
              <div className="p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300">
                <div className="text-3xl md:text-4xl font-bold text-coldIndigo dark:text-glacierBlue mb-2">
                  <span className="counter" data-target={stat.number}>
                    {stat.number}
                  </span>
                  {stat.suffix}
                </div>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70">
                  {stat.label}
                </p>
              </div>
            </div>
          ))}
        </div>

        {/* Ricerca e Sviluppo */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-4xl font-bold text-center mb-12 text-coldIndigo dark:text-glacierBlue">
            Ricerca e Sviluppo - Il Cuore dell&apos;Innovazione
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            {[
              {
                icon: "🧠",
                title: "PolarAi™ - IA Proprietaria",
                desc: "Intelligenza Artificiale interna sviluppata completamente in-house, specializzata in modelli di machine learning avanzati e algoritmi predittivi per l'elaborazione intelligente di big data complessi",
              },
              {
                icon: "⚡",
                title: "Architetture Innovative",
                desc: "Ricerca continua su infrastrutture distribuite e sistemi edge computing per il processing real-time di grandi volumi di dati",
              },
              {
                icon: "🔒",
                title: "Privacy-by-Design",
                desc: "Metodologie innovative per l'anonimizzazione avanzata e la protezione totale dei dati sensibili",
              },
              {
                icon: "🔬",
                title: "Innovation Lab",
                desc: "Sperimentazione continua di tecnologie emergenti e sistemi di intelligenza artificiale di nuova generazione",
              },
            ].map((item, index) => (
              <div
                key={index}
                className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
              >
                <div className="text-4xl mb-4">{item.icon}</div>
                <h4 className="text-xl font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {item.title}
                </h4>
                <p className="text-polarNight/70 dark:text-articWhite/70 leading-relaxed">
                  {item.desc}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Mission Statement */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-4xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            Il Futuro che ci definisce
          </h3>
          <div className="max-w-4xl mx-auto text-center">
            <blockquote className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-8 italic">
              &quot;La missione di DataPolar è migliorare concretamente la vita
              quotidiana delle persone attraverso l&apos;analisi scrupolosa e
              l&apos;elaborazione intelligente dei dati tramite tecnologie
              avanzate di Intelligenza Artificiale (AI) sviluppate
              internamente&quot;
            </blockquote>
          </div>
        </div>

        {/* Ambiti di Applicazione */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-12 text-coldIndigo dark:text-glacierBlue">
            Ambiti di Applicazione
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[
              {
                icon: "🚗",
                title: "Mobilità e Trasporti",
                desc: "Miglioriamo l'efficienza del trasporto pubblico e privato, ottimizzando flussi e infrastrutture",
              },
              {
                icon: "⚡",
                title: "Energia e Sostenibilità",
                desc: "Ottimizziamo la gestione delle reti energetiche per aumentare l'efficienza e ridurre i consumi",
              },
              {
                icon: "🔰️",
                title: "Sicurezza Stradale",
                desc: "Analizziamo dati in tempo reale per potenziare la sicurezza e prevenire incidenti",
              },
              {
                icon: "🏙️",
                title: "Smart Cities",
                desc: "Sviluppiamo sistemi intelligenti per città connesse e abitazioni smart",
              },
              {
                icon: "📈",
                title: "Marketing Comportamentale",
                desc: "Studiamo comportamenti e preferenze per prodotti e servizi personalizzati",
              },
              {
                icon: "🏭",
                title: "Industria 4.0",
                desc: "Favoriamo la trasformazione digitale attraverso soluzioni data-driven",
              },
              {
                icon: "🛒",
                title: "Retail e Distribuzione",
                desc: "Ottimizziamo operazioni commerciali, inventario e logistica",
              },
              {
                icon: "🏥",
                title: "Salute e Benessere",
                desc: "Supportiamo soluzioni innovative nella sanità per cure personalizzate",
              },
              {
                icon: "🔍",
                title: "Analisi Predittiva",
                desc: "Prevediamo tendenze e comportamenti futuri attraverso modelli di machine learning avanzati",
              },
            ].map((area, index) => (
              <div
                key={index}
                className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
              >
                <div className="text-3xl mb-4">{area.icon}</div>
                <h4 className="text-lg font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {area.title}
                </h4>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70 leading-relaxed">
                  {area.desc}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Partnerships */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            Partnerships B2B
          </h3>
          <div className="max-w-4xl mx-auto">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10">
                <h4 className="text-xl font-semibold mb-4 text-coldIndigo dark:text-glacierBlue">
                  Collaborazioni Strategiche
                </h4>
                <p className="text-polarNight/80 dark:text-articWhite/80 leading-relaxed">
                  Operiamo esclusivamente attraverso partnership commerciali
                  B2B, collaborando con aziende e realtà industriali di ogni
                  settore per produrre soluzioni personalizzate basate sui dati
                </p>
              </div>
              <div className="p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10">
                <h4 className="text-xl font-semibold mb-4 text-coldIndigo dark:text-glacierBlue">
                  Studi Professionali
                </h4>
                <p className="text-polarNight/80 dark:text-articWhite/80 leading-relaxed">
                  Offriamo documentazione automatizzata e AI-powered per studi
                  legali, commercialisti e revisori, con report generati da IA
                  locale nel rispetto del GDPR
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Sovranità dei Dati */}
        <div className="mb-20 animate-on-scroll">
          <h3 className="text-2xl md:text-3xl font-bold text-center mb-8 text-coldIndigo dark:text-glacierBlue">
            Sovranità dei Dati e AI Privata
          </h3>
          <div className="max-w-4xl mx-auto">
            <div className="p-8 bg-gradient-to-r from-coldIndigo/10 to-glacierBlue/10 backdrop-blur-sm rounded-2xl border border-coldIndigo/20">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {[
                  "🌍 IA eseguita in locale su server privati in Europa",
                  "🚫 Nessun dato trasmesso a OpenAI, Google o Amazon",
                  "🔒 Documentazione generata senza uscita di dati sensibili",
                  "🏠 Modelli ospitati su infrastruttura dedicata",
                  "📊 Processo auditabile e tracciato end-to-end",
                  "🔰️️ Privacy-by-design e local AI execution",
                ].map((feature, index) => (
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
            Certificazioni e Standard di Eccellenza
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {[
              {
                icon: "🏛️",
                title: "Tech Company Certificata",
                desc: "Società certificata per ricerca e sviluppo di tecnologie innovative",
              },
              {
                icon: "🔐",
                title: "GDPR Compliance",
                desc: "Conformità integrale al Regolamento Europeo sulla Privacy",
              },
              {
                icon: "🔬",
                title: "R&D Interna",
                desc: "Ricerca e sviluppo focalizzate su PolarAi™ e tecnologie proprietarie",
              },
            ].map((cert, index) => (
              <div
                key={index}
                className="card-stagger text-center p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-gray-300 dark:border-white/10 hover:border-coldIndigo/30 transition-all duration-300"
              >
                <div className="text-4xl mb-4">{cert.icon}</div>
                <h4 className="text-lg font-semibold mb-3 text-coldIndigo dark:text-glacierBlue">
                  {cert.title}
                </h4>
                <p className="text-sm text-polarNight/70 dark:text-articWhite/70">
                  {cert.desc}
                </p>
              </div>
            ))}
          </div>
        </div>

        {/* Call to Action finale */}
        <div className="text-center animate-on-scroll">
          <div className="p-8 bg-gradient-to-r from-coldIndigo/20 to-glacierBlue/20 backdrop-blur-sm rounded-2xl border border-coldIndigo/30">
            <h3 className="text-2xl md:text-3xl font-bold mb-4 text-coldIndigo dark:text-glacierBlue">
              PolarDrive™
            </h3>
            <p className="text-lg text-polarNight/80 dark:text-articWhite/80 mb-6">
              La scelta naturale per le aziende che vogliono trasformare i
              propri dati in vantaggio competitivo attraverso tecnologie
              sostenibili
            </p>
            <button
              onClick={navigateToPolarDrive}
              className="px-8 py-4 bg-coldIndigo text-white font-semibold rounded-full hover:scale-105 transition-all duration-300 hover:shadow-xl hover:shadow-coldIndigo/30"
            >
              Scopri PolarDrive™
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
