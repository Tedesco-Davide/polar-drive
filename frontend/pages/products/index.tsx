"use client";

import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import { useTheme } from "next-themes";
import { useEffect, useState } from "react";
import Head from "next/head";
import Header from "@/components/header";
import Link from "next/link";
import Image from "next/image";
import classNames from "classnames";

export default function Products() {
  const { t } = useTranslation();
  const { theme } = useTheme();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  const products = [
    {
      id: "polardrive",
      name: "PolarDrive",
      description: t("products.polardrive.description"),
      image: "/logo/DataPolar_Logo_PolarDrive.png",
      href: "/products/polardrive",
    },
  ];

  return (
    <>
      <Head>
        <title>{t("app.title.products")}</title>
      </Head>
      <Header />
      <section className="relative w-full overflow-hidden min-h-[100vh] pt-28 pb-10 px-6 scroll-mt-16">
        {/* Background dinamico di sfondo */}
        {mounted && (
          <div
            className={classNames("absolute inset-0 z-0", {
              "bg-products-hero-light": theme === "light",
              "dark:bg-products-hero": theme === "dark",
            })}
          />
        )}

        <div className="container mx-auto text-center relative z-10">
          <h1 className="text-4xl md:text-5xl font-bold text-polarNight dark:text-softWhite mb-8">
            {t("products.title")}
          </h1>
          <p className="text-lg md:text-xl text-gray-700 dark:text-gray-300 max-w-2xl mx-auto mb-12">
            {t("products.subtitle")}
          </p>

          <div className="grid gap-10">
            {products.map((product) => (
              <Link
                key={product.id}
                href={product.href}
                className="flex flex-col md:flex-row items-center md:items-start bg-softWhite dark:bg-gray-800 rounded-2xl shadow-md hover:shadow-xl transition-all border border-gray-100 dark:border-gray-700 overflow-hidden"
              >
                <div className="relative w-full md:w-1/3 h-48 md:h-56 bg-gray-800 flex items-center justify-center">
                  <Image
                    src={product.image}
                    alt={product.name}
                    fill
                    className="object-contain"
                  />
                </div>

                <div className="p-6 text-left md:w-2/3">
                  <h3 className="text-xl font-semibold text-polarNight dark:text-softWhite">
                    {product.name}
                  </h3>
                  <p className="text-gray-600 dark:text-gray-400 mt-2 text-sm">
                    {product.description}
                  </p>
                </div>
              </Link>
            ))}
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
