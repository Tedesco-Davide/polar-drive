import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTranslation } from "next-i18next";
import Head from "next/head";
import Header from "@/components/header";
import Hero from "@/components/hero";
import Mission from "@/components/mission";
import Contacts from "@/components/contacts";

export default function Home() {
  const { t } = useTranslation("");

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
