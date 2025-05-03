import { useTranslation } from "next-i18next";

export default function Mission() {
  const { t } = useTranslation();

  return (
    <section
      id="mission"
      className="relative w-full overflow-hidden py-24 px-6 scroll-mt-16"
    >
      {/* Sfondo sfumato indigo fisso */}
      <div className="absolute inset-0 z-0 bg-gradient-radial from-coldIndigo/40 via-coldIndigo/20 to-transparent" />

      {/* Alone centrale */}
      <div className="absolute inset-0 z-10 pointer-events-none">
        <div className="absolute left-1/2 top-32 -translate-x-1/2 w-[600px] h-[600px] rounded-full bg-[#5c4de14a] dark:bg-[#5c4de130] blur-3xl opacity-60" />
      </div>

      {/* Contenuto */}
      <div className="relative z-20 max-w-4xl mx-auto text-center text-polarNight dark:text-articWhite space-y-8">
        <h2 className="text-4xl md:text-6xl font-bold">{t("mission.title")}</h2>
        <p className="text-lg leading-relaxed">{t("mission.paragraph1")}</p>
        <p className="text-lg leading-relaxed">{t("mission.paragraph2")}</p>
      </div>
    </section>
  );
}
