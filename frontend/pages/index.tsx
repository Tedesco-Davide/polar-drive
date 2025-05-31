import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import { useEffect } from "react";
import { useRouter } from "next/router";
import { logFrontendEvent } from "@/utils/logger";
import Head from "next/head";
import Header from "@/components/header";
import Hero from "@/components/hero";
import Mission from "@/components/mission";
import Contacts from "@/components/contacts";

export default function Home() {
  const { t } = useTranslation("");
  const router = useRouter();

  useEffect(() => {
    logFrontendEvent(
      "PublicLanding",
      "INFO",
      "Landing page loaded",
      `Lang: ${router.locale}, URL: ${window.location.href}`
    );
  }, [router.locale]);

  return (
    <>
      <Head>
        <title>{t("app.title.home-mission-contacts")}</title>
      </Head>
      <Header />
      <Hero />
      <Mission />
      <Contacts />
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
