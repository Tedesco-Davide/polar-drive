"use client";

import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
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
  const router = useRouter();
  const [mounted, setMounted] = useState(false);
  const heroRef = useRef<HTMLElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const quoteRef = useRef<HTMLParagraphElement>(null);
  const particlesRef = useRef<HTMLDivElement>(null);

  const benefits = [
    {
      icon: Brain,
      title: "Analisi automatizzate e continuative",
      description:
        "Grazie a un motore AI-based, i dati raccolti vengono elaborati e sintetizzati in report periodici intelligenti, pronti per essere utilizzati in contesti operativi, amministrativi o decisionali che richiedono documentazione certificata",
    },
    {
      icon: Battery,
      title: "Supporto alla transizione ecologica",
      description:
        "I dati aggregati contribuiscono a individuare aree di miglioramento energetico, ottimizzare i percorsi, limitare le emissioni indirette e favorire l'adozione di comportamenti qualificabili per incentivi e agevolazioni a basso impatto",
    },
    {
      icon: Radar,
      title: "Monitoraggio avanzato del territorio",
      description:
        "Ottenuto mappando in modo dinamico il comportamento dei veicoli elettrici connessi, fornendo insight preziosi per la progettazione urbana sostenibile, le reti infrastrutturali, i punti di ricarica e l'identificazione di opportunità economiche strategiche legate all'evoluzione delle abitudini di spostamento",
    },
    {
      icon: Shield,
      title: "Protezione e anonimizzazione totale",
      description:
        "Ogni dato viene trattato secondo i più rigorosi standard europei. Il sistema è GDPR-proof by design e genera documentazione che soddisfa requisiti normativi specifici per trasparenza e tracciabilità",
    },
    {
      icon: FileText,
      title: "Valore documentale",
      description:
        "Generando documentazione certificabile strategicamente rilevante per supportare audit, rendicontazioni, e attività conformi a normative di settore con implicazioni economiche vantaggiose, in ottica di trasparenza e tracciabilità",
    },
    {
      icon: TrendingUp,
      title: "Intelligence predittiva avanzata",
      description:
        "Algoritmi di machine learning elaborano pattern comportamentali per prevedere tendenze operative future e identificare inefficienze nascoste. Le analisi predittive facilitano investimenti strategici qualificati e supportano la pianificazione di lungo termine con vantaggi competitivi documentabili",
    },
  ];

  const targetAudience = [
    {
      icon: Building2,
      title: "Comparti industriali e logistici",
      description: "Efficienza operativa e controllo della supply chain",
    },
    {
      icon: Zap,
      title: "Servizi di mobilità sostenibile",
      description:
        "Tecnologie intelligenti per l'ecosistema della mobilità green",
    },
    {
      icon: Truck,
      title: "Gestori e fornitori di flotte elettriche",
      description: "Monitoraggio e ottimizzazione delle flotte aziendali",
    },
    {
      icon: Landmark,
      title: "Comuni, province, enti pubblici e privati",
      description: "Raccolta dati territoriali per decisioni strategiche",
    },
    {
      icon: Briefcase,
      title: "Holdings e gruppi societari",
      description:
        "Soluzioni scalabili per diversificazione strategica e gestione integrata multi-business",
    },
    {
      icon: Users,
      title: "Società di investimento e fondi",
      description:
        "Intelligence data-driven per portafogli sostenibili con performance economiche ottimizzate",
    },
    {
      icon: Crown,
      title: "Grandi corporazioni e multinazionali",
      description:
        "Strategie ESG integrate con impatti patrimoniali misurabili e strutturati",
    },
    {
      icon: Gem,
      title: "Family office e gestioni patrimoniali",
      description:
        "Veicoli di investimento green con gestione patrimoniale efficiente e trasparente",
    },
  ];

  const complianceFeatures = [
    "Conformità al Regolamento GDPR (art. 5, 6, 25, 32, 35)",
    "Protezione dei dati by design, con minimizzazione e anonimizzazione integrata",
    "Trattamento delle informazioni esclusivamente in forma aggregata e cifrata",
    "Raccolta e conservazione dei consensi secondo standard verificabili e certificabili",
  ];

  const securityFeatures = [
    "Tutte le interazioni sono cifrate con protocolli end-to-end",
    "Il motore di raccolta è progettato per essere leggero, isolato e sicuro",
    "Policy di cyber hygiene per ridurre i rischi digitali associati alla mobilità connessa",
    "Protezione dall'esposizione involontaria a violazioni o trattamenti impropri",
  ];

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
    if (mounted) {
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
              },
            }
          );
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
  }, [mounted]);

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
      // Se non siamo sulla homepage, naviga alla homepage con l'anchor
      window.location.href = `/${anchorTarget}`;
    } else {
      // Se siamo già sulla homepage, scrolla direttamente alla sezione
      const element = document.querySelector(anchorTarget);
      if (element) {
        element.scrollIntoView({ behavior: "smooth" });
      }
    }
  };

  return (
    <>
      <Head>
        <title>
          PolarDrive™ - Piattaforma Intelligente per Mobilità Sostenibile |
          DataPolar
        </title>
        <meta
          name="description"
          content="PolarDrive™: la piattaforma intelligente che trasforma il movimento in conoscenza. Gestione automatizzata di veicoli elettrici connessi per analisi di sostenibilità e efficienza energetica."
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

        {/* Hero Section Unificata */}
        <section
          ref={heroRef}
          className="relative w-full overflow-hidden min-h-screen flex items-center pt-16"
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

          {/* Content */}
          <div className="relative z-20 container mx-auto p-6">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 md:gap-12 items-center">
              {/* Text Content */}
              <div className="space-y-8">
                <div>
                  <h1
                    ref={titleRef}
                    className="text-4xl md:text-6xl lg:text-7xl font-bold mb-6 leading-tight"
                  >
                    <span className="bg-gradient-to-r from-coldIndigo via-glacierBlue to-coldIndigo bg-clip-text text-transparent">
                      PolarDrive™
                    </span>
                  </h1>

                  <blockquote className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-8 italic border-l-4 border-coldIndigo pl-6">
                    &quot;Il tramite per un futuro del pianeta migliore, che
                    trasforma il movimento in conoscenza. Un futuro in cui ogni
                    veicolo elettrico diventa un centro di intelligenza
                    attiva&quot;
                  </blockquote>
                </div>

                <div className="space-y-6">
                  <p className="text-lg md:text-xl leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                    Ogni percorso, ogni interazione con l&apos;ambiente, ogni
                    istante operativo viene trasformato in dati oggettivi. Dati
                    che aiutano a prendere decisioni più consapevoli, a
                    migliorare l&apos;impatto ambientale, ed a rendere le
                    aziende più efficienti e trasparenti
                  </p>

                  {/* Sezione "Cos'è PolarDrive" integrata */}
                  <div className="border-t border-coldIndigo/30 pt-8">
                    <h2 className="text-2xl md:text-3xl font-bold mb-4 text-coldIndigo dark:text-glacierBlue">
                      Cos&apos;è PolarDrive™
                    </h2>
                    <div className="space-y-4 text-base md:text-lg leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                      <p>
                        E&lsquo; una piattaforma intelligente sviluppata da
                        DataPolar per la gestione automatizzata di veicoli
                        elettrici connessi, utilizzati per la raccolta, analisi
                        e valorizzazione di dati ambientali, operativi e
                        logistici
                      </p>
                      <p>
                        Integrandosi direttamente con le principali piattaforme
                        API ufficiali di veicoli elettrici di nuova generazione,
                        trasforma ogni dispositivo mobile compatibile in una
                        fonte costante di dati strutturati, raccolti con cadenza
                        continua ed utilizzabili per analisi di efficienza
                        energetica, sostenibilità, pianificazione strategica
                        digitale e territoriale avanzate
                      </p>
                      <p>
                        Attraverso l&apos;automazione totale della raccolta e la
                        generazione ricorrente di report intelligenti,
                        PolarDrive™ consente una tracciabilità completa e
                        certificabile delle dinamiche operative legate alla
                        mobilità elettrica
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              {/* Product Image */}
              <div className="relative mt-8 lg:mt-0">
                <div className="relative w-full h-64 md:h-80 lg:h-96 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-2xl flex items-center justify-center backdrop-blur-sm border border-white/10 mb-14 lg:mb-0">
                  <div className="relative w-full h-full p-6 md:p-8">
                    <Image
                      src="/logo/PolarDrive_Logo.svg"
                      alt="PolarDrive Logo"
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
        </section>

        {/* Vision Section */}
        <section className="relative w-full overflow-hidden py-24 px-6">
          <div className="absolute inset-0 z-0">
            <div className="absolute top-1/4 right-1/3 w-[400px] h-[400px] rounded-full bg-glacierBlue/10 blur-3xl animate-pulse" />
          </div>

          <div className="relative z-20 max-w-5xl mx-auto text-center animate-on-scroll">
            <h2 className="text-3xl mud:text-5xl font-bold mb-8 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent">
              Il miglior alleato per la tua Evoluzione Aziendale
            </h2>

            <div className="p-8 bg-gradient-to-r from-coldIndigo/10 to-glacierBlue/10 backdrop-blur-sm rounded-3xl border border-coldIndigo/20">
              <p className="text-xl md:text-2xl font-semibold text-polarNight dark:text-articWhite mb-6">
                Trasformiamo insieme i dati per un&apos;economia più
                trasparente, efficiente e sostenibile
              </p>
              <p className="text-lg leading-relaxed text-polarNight/80 dark:text-articWhite/80">
                Con PolarDrive™ qualunque attività svolta da un veicolo
                elettrico connesso, diventa parte di un ecosistema intelligente,
                in cui ogni chilometro percorso, ogni sosta, ogni condizione
                operativa viene tracciata in tempo reale per generare valore
                informativo, migliorare i processi e contribuire a un impatto
                ambientale positivo
              </p>
            </div>
          </div>
        </section>

        {/* Benefits Section */}
        <section className="relative w-full overflow-hidden py-24 px-6">
          <div className="absolute inset-0 z-0">
            <div className="absolute top-1/3 left-1/4 w-[400px] h-[400px] rounded-full bg-coldIndigo/10 blur-3xl animate-pulse" />
          </div>

          <div className="relative z-20 max-w-7xl mx-auto">
            <h2 className="text-3xl md:text-5xl font-bold text-center mb-16 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent animate-on-scroll">
              I Benefici di PolarDrive™
            </h2>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
              {benefits.map((benefit, index) => {
                const Icon = benefit.icon;
                return (
                  <div
                    key={index}
                    className="card-stagger p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group"
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
        <section className="relative w-full overflow-hidden py-24 px-6">
          <div className="relative z-20 max-w-6xl mx-auto">
            <h2 className="text-3xl md:text-5xl font-bold text-center mb-16 bg-gradient-to-r from-coldIndigo to-glacierBlue bg-clip-text text-transparent animate-on-scroll">
              A chi è rivolto PolarDrive™
            </h2>

            <p className="text-lg text-center mb-12 text-polarNight/80 dark:text-articWhite/80 max-w-4xl mx-auto animate-on-scroll">
              Progettato per essere integrato in ogni business, in cui veicoli
              elettrici connessi vogliano essere utilizzati come parte
              operativa, strategica o analitica dei processi aziendali
            </p>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {targetAudience.map((target, index) => {
                const Icon = target.icon;
                return (
                  <div
                    key={index}
                    className="card-stagger p-6 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-xl border border-white/10 hover:border-coldIndigo/30 transition-all duration-300 group text-center"
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
        <section className="relative w-full overflow-hidden py-24 px-6">
          <div className="relative z-20 max-w-6xl mx-auto">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-12">
              {/* Legal Compliance */}
              <div className="animate-on-scroll">
                <div className="p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-white/10">
                  <div className="flex items-center mb-6">
                    <div className="w-12 h-12 mr-4 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-xl flex items-center justify-center">
                      <Award className="w-6 h-6 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h3 className="text-2xl font-bold text-coldIndigo dark:text-glacierBlue">
                      Legalità e conformità al centro
                    </h3>
                  </div>
                  <p className="text-polarNight/80 dark:text-articWhite/80 mb-6">
                    PolarDrive™ è progettato per operare in pieno allineamento
                    con le più recenti normative europee su privacy, ambiente e
                    tecnologie digitali
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
                <div className="p-8 bg-white/5 dark:bg-white/5 backdrop-blur-sm rounded-2xl border border-white/10">
                  <div className="flex items-center mb-6">
                    <div className="w-12 h-12 mr-4 bg-gradient-to-br from-coldIndigo/20 to-glacierBlue/20 rounded-xl flex items-center justify-center">
                      <Lock className="w-6 h-6 text-coldIndigo dark:text-glacierBlue" />
                    </div>
                    <h3 className="text-2xl font-bold text-coldIndigo dark:text-glacierBlue">
                      Cybersecurity e integrità operativa
                    </h3>
                  </div>
                  <p className="text-polarNight/80 dark:text-articWhite/80 mb-6">
                    PolarDrive™ non è solo raccolta dati: è anche protezione
                    delle infrastrutture informative mobili
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
            <div className="absolute inset-0 bg-gradient-to-br from-coldIndigo/10 via-glacierBlue/10 to-transparent" />
          </div>

          <div className="relative z-20 max-w-4xl mx-auto text-center animate-on-scroll">
            <div className="p-12 bg-gradient-to-r from-coldIndigo/20 to-glacierBlue/20 backdrop-blur-sm rounded-3xl border border-coldIndigo/30">
              <Compass className="w-16 h-16 mx-auto mb-6 text-coldIndigo dark:text-glacierBlue" />
              <h3 className="text-3xl md:text-4xl font-bold mb-6 text-coldIndigo dark:text-glacierBlue">
                Naviga con DataPolar verso il futuro ed ottimizza le tue risorse
              </h3>
              <p className="text-lg text-polarNight/80 dark:text-articWhite/80 mb-8 max-w-2xl mx-auto">
                Scopri come PolarDrive™ può convertire le tue spese operative in
                investimenti intelligenti con ritorni misurabili e vantaggi
                economici strutturati. <br /> La partnership che ridefinisce il
                ROI della tua organizzazione
              </p>
              <button
                onClick={scrollToContacts}
                className="inline-flex items-center gap-3 px-10 py-5 bg-coldIndigo text-white font-semibold rounded-full transition-all duration-300 hover:scale-105 hover:shadow-xl hover:shadow-coldIndigo/30 group"
              >
                <span>Diventa Partner</span>
                <ArrowRight className="w-5 h-5 transition-transform duration-300 group-hover:translate-x-1" />
              </button>
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
