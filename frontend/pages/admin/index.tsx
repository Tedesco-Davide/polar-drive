import { useEffect, useState } from "react";
import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import { logFrontendEvent } from "@/utils/logger";
import TabVehicleWorkflow from "@/components/vehicleWorkflow/tabVehicleWorkflow";
import TabPolarReports from "@/components/polarReports/tabPolarReports";
import TabOutages from "@/components/outages/tabOutages";
import TabClientCompanies from "@/components/clientCompanies/tabClientCompanies";
import TabFileManager from "@/components/fileManager/tabFileManager";
import Head from "next/head";
import classNames from "classnames";
import LayoutMainHeader from "@/components/generic/layoutMainHeader";

export default function AdminDashboard() {
  const FAKE_AUTH = true;

  type AdminTab = "TabVehicleWorkflow" | "TabPolarReports" | "TabOutages" | "TabClientCompanies" | "TabFileManager";
  const [activeTab, setActiveTab] = useState<AdminTab>("TabVehicleWorkflow");
  const [mounted, setMounted] = useState(false);
  const { theme } = useTheme();
  const { t } = useTranslation("common");

  useEffect(() => {
    if (!FAKE_AUTH) {
      window.location.href = "/api/auth/signin";
    }
  }, [FAKE_AUTH]);

  useEffect(() => {
    setMounted(true);
    logFrontendEvent(
      "AdminDashboard",
      "INFO",
      "Admin dashboard mounted and visible",
    );
    // Load saved tab from localStorage after mount to avoid hydration mismatch
    const saved = localStorage.getItem("adminActiveTab");
    if (saved === "TabVehicleWorkflow" || saved === "TabPolarReports" || saved === "TabOutages" || saved === "TabClientCompanies" || saved === "TabFileManager") {
      setActiveTab(saved);
    }
  }, []);

  useEffect(() => {
    localStorage.setItem("adminActiveTab", activeTab);
  }, [activeTab]);

  return (
    <>
      <Head>
        <title>{t("admin.title")}</title>
      </Head>
      <>
        <LayoutMainHeader />
        <section className="relative w-full h-screen pt-[64px] overflow-hidden">
          <div className="h-full overflow-y-auto px-6">
            <div suppressHydrationWarning>
              {mounted && (
                <div
                  className={classNames(
                    "absolute inset-0 z-0 bg-background bg-[length:40px_40px] pointer-events-none",
                    {
                      "bg-products-grid-light": theme === "light",
                      "dark:bg-products-grid": theme === "dark",
                    },
                  )}
                />
              )}
            </div>

            <div className="relative z-20 mx-auto">
              <div className="relative z-20 mx-auto border-y-8 border-gray-300 dark:border-gray-600 py-3 my-6">
                <p className="text-3xl font-medium text-gray-600 dark:text-softWhite">
                  {t("admin.title")}
                </p>
              </div>
              <div className="mb-12 ring-1 ring-inset ring-gray-300 dark:ring-gray-600 rounded">
                <button
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "TabVehicleWorkflow",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "TabVehicleWorkflow",
                    },
                  )}
                  onClick={() => setActiveTab("TabVehicleWorkflow")}
                >
                  {t("admin.tabWorkflow")}
                </button>
                <button
                  disabled={false}
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "TabPolarReports",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "TabPolarReports",
                    },
                  )}
                  onClick={() => setActiveTab("TabPolarReports")}
                >
                  {t("admin.tabPolarReports")}
                </button>
                <button
                  disabled={false}
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "TabOutages",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "TabOutages",
                    },
                  )}
                  onClick={() => setActiveTab("TabOutages")}
                >
                  {t("admin.tabOutages")}
                </button>
                <button
                  disabled={false}
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "TabClientCompanies",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "TabClientCompanies",
                    },
                  )}
                  onClick={() => setActiveTab("TabClientCompanies")}
                >
                  {t("admin.tabClientCompanies")}
                </button>
                <button
                  disabled={false}
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "TabFileManager",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "TabFileManager",
                    },
                  )}
                  onClick={() => setActiveTab("TabFileManager")}
                >
                  {t("admin.tabFileManager")}
                </button>
              </div>

              {activeTab === "TabVehicleWorkflow" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto lg:min-w-fit mb-12">
                    <div className="grid grid-cols-1 gap-6">
                      <TabVehicleWorkflow t={t} />
                    </div>
                  </div>
                </div>
              )}

              {activeTab === "TabPolarReports" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto lg:min-w-fit mb-12">
                    <div className="grid grid-cols-1 gap-6">
                      <TabPolarReports t={t} />
                    </div>
                  </div>
                </div>
              )}

              {activeTab === "TabOutages" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto lg:min-w-fit mb-12">
                    <div className="grid grid-cols-1 gap-6">
                      <TabOutages t={t} />
                    </div>
                  </div>
                </div>
              )}

              {activeTab === "TabClientCompanies" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto lg:min-w-fit mb-12">
                    <div className="grid grid-cols-1 gap-6">
                      <TabClientCompanies t={t} />
                    </div>
                  </div>
                </div>
              )}

              {activeTab === "TabFileManager" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto lg:min-w-fit mb-12">
                    <div className="grid grid-cols-1 gap-6">
                      <TabFileManager t={t} />
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </section>
      </>
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
