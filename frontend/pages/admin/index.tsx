import { useEffect, useState } from "react";
import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import { logFrontendEvent } from "@/utils/logger";
import AdminClientVehiclesTable from "@/components/adminClientVehiclesTable";
import AdminClientCompaniesTable from "@/components/adminClientCompaniesTable";
import AdminMainWorkflow from "@/components/adminMainWorkflow";
import AdminGapAlertsDashboard from "@/components/adminGapAlertsDashboard";
import AdminClientConsents from "@/components/adminClientConsentsTable";
import AdminOutagePeriodsTable from "@/components/adminOutagePeriodsTable";
import AdminFileManagerTable from "@/components/adminFileManager";
import AdminPdfReports from "@/components/adminPdfReports";
import Head from "next/head";
import Header from "@/components/header";
import classNames from "classnames";

export default function AdminDashboard() {
  const FAKE_AUTH = true;

  type AdminTab = "PolarDrive" | "ComingSoon";
  const [activeTab, setActiveTab] = useState<AdminTab>("PolarDrive");
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
  }, []);

  return (
    <>
      <Head>
        <title>{t("admin.title")}</title>
      </Head>
      <>
        <Header />
        <section className="relative w-full h-screen pt-[64px] overflow-hidden">
          <div className="h-full overflow-y-auto px-6">
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

            <div className="relative z-20 mx-auto">
              <div className="relative z-20 mx-auto border-y-8 border-gray-300 dark:border-gray-600 py-3 my-6">
                <p className="text-3xl font-medium text-gray-600 dark:text-softWhite">
                  {t("admin.title")}
                </p>
              </div>
              <div className="mb-12 ring-1 ring-inset ring-gray-300 dark:ring-gray-600 rounded">
                <button
                  disabled={true}
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "ComingSoon",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "ComingSoon",
                    },
                  )}
                  onClick={() => setActiveTab("ComingSoon")}
                >
                  {t("admin.tabDashboard")}
                </button>
                <button
                  className={classNames(
                    "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                    {
                      "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                        activeTab === "PolarDrive",
                      "border-transparent text-gray-500 hover:text-primary":
                        activeTab !== "PolarDrive",
                    },
                  )}
                  onClick={() => setActiveTab("PolarDrive")}
                >
                  {t("admin.tabWorkflow")}
                </button>
              </div>

              {activeTab === "PolarDrive" && (
                <div className="overflow-x-auto">
                  <div className="mx-auto space-y-12 lg:min-w-fit mb-12">
                    <AdminGapAlertsDashboard t={t} />
                    <AdminMainWorkflow />
                    <AdminClientCompaniesTable t={t} />
                    <AdminClientVehiclesTable t={t} />
                    <AdminClientConsents t={t} />
                    <AdminOutagePeriodsTable t={t} />
                    <AdminPdfReports t={t} />
                    <AdminFileManagerTable t={t} />
                  </div>
                </div>
              )}

              {activeTab === "ComingSoon" && (
                <div>
                  <p className="text-xl text-gray-600 dark:text-softWhite">
                    Stay tuned!
                  </p>
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
