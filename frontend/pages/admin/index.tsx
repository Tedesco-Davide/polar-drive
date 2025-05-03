import { useEffect, useState } from "react";
import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { ClientTeslaVehicle } from "@/types/teslaVehicleInterfaces";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import { PdfReport } from "@/types/reportInterfaces";
import AdminLoader from "@/components/adminLoader";
import AdminClientTeslaVehiclesTable from "@/components/adminClientTeslaVehiclesTable";
import AdminClientCompaniesTable from "@/components/adminClientCompaniesTable";
import AdminMainWorkflow from "@/components/adminMainWorkflow";
import AdminClientConsents from "@/components/adminClientConsentsTable";
import AdminOutagePeriodsTable from "@/components/adminOutagePeriodsTable";
import AdminTeslaVehiclesReport from "@/components/adminTeslaVehiclesReport";
import Head from "next/head";
import Header from "@/components/header";
import classNames from "classnames";

export default function AdminDashboard() {
  const FAKE_AUTH = true;

  type AdminTab = "PolarDrive" | "ComingSoon";
  const [activeTab, setActiveTab] = useState<AdminTab>("PolarDrive");

  const [clients, setClients] = useState<ClientCompany[]>([]);
  const [vehicles, setVehicles] = useState<ClientTeslaVehicle[]>([]);
  const [clientConsents, setClientConsents] = useState<ClientConsent[]>([]);
  const [outagePeriods, setOutagePeriods] = useState<OutagePeriod[]>([]);
  const [pdfReports, setPdfReports] = useState<PdfReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [mounted, setMounted] = useState(false);
  const { theme } = useTheme();
  const { t } = useTranslation("common");

  useEffect(() => {
    if (!FAKE_AUTH) {
      window.location.href = "/api/auth/signin";
    }
  }, []);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    const fetchAll = async () => {
      try {
        const [clientsRes, vehiclesRes, consentsRes, outagesRes, reportsRes] =
          await Promise.all([
            fetch("/api/mock-clientcompanies"),
            fetch("/api/mock-vehicles"),
            fetch("/api/mock-clientconsents"),
            fetch("/api/mock-outageperiods"),
            fetch("/api/mock-pdfreports"),
          ]);
        const [
          clientsData,
          vehiclesData,
          consentsData,
          outagesData,
          reportsData,
        ] = await Promise.all([
          clientsRes.json(),
          vehiclesRes.json(),
          consentsRes.json(),
          outagesRes.json(),
          reportsRes.json(),
        ]);
        setClients(clientsData);
        setVehicles(vehiclesData);
        setClientConsents(consentsData);
        setOutagePeriods(outagesData);
        setPdfReports(reportsData);
      } catch (err) {
        console.error("API fetch error:", err);
      } finally {
        setLoading(false);
      }
    };
    fetchAll();
  }, []);

  return (
    <>
      <Head>
        <title>{t("admin.title")}</title>
      </Head>

      {loading && <AdminLoader />}

      {!loading && (
        <>
          <Header />
          <section className="relative w-full h-screen pt-[64px] overflow-hidden">
            <div className="h-full overflow-y-auto px-6">
              {mounted && (
                <div
                  className={classNames(
                    "absolute inset-0 z-0 bg-background bg-[length:40px_40px]",
                    {
                      "bg-products-grid-light": theme === "light",
                      "dark:bg-products-grid": theme === "dark",
                    }
                  )}
                />
              )}

              <div className="absolute inset-0 z-10 pointer-events-none">
                <div className="absolute left-1/2 top-32 -translate-x-1/2 w-[600px] h-[600px] rounded-full bg-[#5c4de14a] dark:bg-[#5c4de130] blur-3xl opacity-60"></div>
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
                          activeTab === "PolarDrive",
                        "border-transparent text-gray-500 hover:text-primary":
                          activeTab !== "PolarDrive",
                      }
                    )}
                    onClick={() => setActiveTab("PolarDrive")}
                  >
                    PolarDrive ❄️🐻‍❄️🚗
                  </button>
                  <button
                    className={classNames(
                      "px-4 py-2 text-2xl font-semibold rounded-t border-b-2 transition-colors duration-200 w-full md:w-fit",
                      {
                        "border-polarNight text-polarNight bg-gray-200 dark:bg-white/10 dark:text-softWhite dark:border-softWhite":
                          activeTab === "ComingSoon",
                        "border-transparent text-gray-500 hover:text-primary":
                          activeTab !== "ComingSoon",
                      }
                    )}
                    onClick={() => setActiveTab("ComingSoon")}
                  >
                    Coming Soon 😎
                  </button>
                </div>

                {activeTab === "PolarDrive" && (
                  <div className="overflow-x-auto">
                    <div className="mx-auto space-y-12 min-w-fit mb-12">
                      <AdminMainWorkflow />
                      <AdminClientCompaniesTable clients={clients} t={t} />
                      <AdminClientTeslaVehiclesTable
                        vehicles={vehicles}
                        t={t}
                      />
                      <AdminClientConsents consents={clientConsents} t={t} />
                      <AdminOutagePeriodsTable outages={outagePeriods} t={t} />
                      <AdminTeslaVehiclesReport reports={pdfReports} t={t} />
                    </div>
                  </div>
                )}

                {activeTab === "ComingSoon" && (
                  <div>
                    <p className="text-xl text-gray-600 dark:text-softWhite">
                      Contenuti per futuro prodotto
                    </p>
                  </div>
                )}
              </div>
            </div>
          </section>
        </>
      )}
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
