"use client";

import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import { useTheme } from "next-themes";
import { useEffect, useState } from "react";
import Head from "next/head";
import Image from "next/image";
import Chip from "@/components/chip";
import Header from "@/components/header";
import classNames from "classnames";

export default function PolarDrivePage() {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  return (
    <>
      <Head>
        <title>{t("app.title.polardrive")}</title>
      </Head>
      <Header />
      <section className="relative w-full overflow-hidden min-h-[100vh] pt-36 pb-10 px-6">
        {/* Sfondo dinamico */}
        {mounted && (
          <div
            className={classNames("absolute inset-0 z-0", {
              "bg-products-hero-light": theme === "light",
              "dark:bg-products-hero": theme === "dark",
            })}
          />
        )}

        {/* Contenuto centrato */}
        <div className="container mx-auto relative z-10">
          {/* Header sezione: immagine + testo */}
          <div className="flex flex-col md:flex-row items-center md:items-start gap-6 mb-16">
            {/* Logo orso */}
            <div className="relative w-24 h-24 md:w-32 md:h-32 flex-shrink-0 bg-gray-800 rounded-xl shadow-md">
              <Image
                src="/logo/DataPolar_Logo_PolarDrive.png"
                alt="PolarDrive dashboard"
                fill
                className="object-contain"
              />
            </div>

            {/* Titolo e descrizione */}
            <div>
              <h1 className="text-4xl md:text-5xl font-bold text-polarNight dark:text-softWhite mb-4">
                {t("polardrive.title")}
              </h1>
              <p className="text-lg md:text-xl text-gray-700 dark:text-gray-300">
                {t("polardrive.subtitle")}
              </p>
            </div>
          </div>

          {/* Vantaggi + Caratteristiche */}
          <div className="grid md:grid-cols-2 gap-10 mb-16">
            {/* Vantaggi */}
            <div>
              <h2 className="text-2xl font-semibold mb-3 text-polarNight dark:text-softWhite">
                {t("polardrive.benefits.title")}
              </h2>
              <ul className="list-disc list-inside space-y-2 text-base text-muted-foreground">
                <li>{t("polardrive.benefits.item1")}</li>
                <li>{t("polardrive.benefits.item2")}</li>
                <li>{t("polardrive.benefits.item3")}</li>
              </ul>
            </div>

            {/* Caratteristiche */}
            <div>
              <h2 className="text-2xl font-semibold mb-3 text-polarNight dark:text-softWhite">
                {t("polardrive.features.title")}
              </h2>
              <ul className="list-disc list-inside space-y-2 text-base text-muted-foreground">
                <li>{t("polardrive.features.item1")}</li>
                <li>{t("polardrive.features.item2")}</li>
                <li>{t("polardrive.features.item3")}</li>
              </ul>
            </div>
          </div>

          {/* Chips + CTA */}
          <div className="flex flex-col items-center gap-4 max-w-xl mx-auto">
            <Chip>{t("polardrive.chips.deductible")}</Chip>
            <Chip>{t("polardrive.chips.compatible")}</Chip>
            <Chip>{t("polardrive.chips.gdpr")}</Chip>
            <button className="mt-8 w-full sm:w-auto px-6 py-3 rounded-full bg-coldIndigo text-articWhite font-medium shadow-md hover:bg-indigo-600 transition">
              {t("polardrive.CTA")}
            </button>
          </div>
        </div>
      </section>
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
