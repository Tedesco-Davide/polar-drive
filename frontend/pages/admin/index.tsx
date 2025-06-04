import { useEffect, useState } from "react";
import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import { API_BASE_URL } from "@/utils/api";
import { WorkflowRow } from "@/types/adminWorkflowTypes";
import { PdfReport } from "@/types/reportInterfaces";
import { ClientVehicleWithCompany as ClientVehicleWithCompany } from "@/types/adminWorkflowTypesExtended";
import { logFrontendEvent } from "@/utils/logger";
import AdminLoader from "@/components/adminLoader";
import AdminClientVehiclesTable from "@/components/adminClientVehiclesTable";
import AdminClientCompaniesTable from "@/components/adminClientCompaniesTable";
import AdminMainWorkflow from "@/components/adminMainWorkflow";
import AdminClientConsents from "@/components/adminClientConsentsTable";
import AdminOutagePeriodsTable from "@/components/adminOutagePeriodsTable";
import AdminPdfReports from "@/components/adminPdfReports";
import Head from "next/head";
import Header from "@/components/header";
import classNames from "classnames";

export default function AdminDashboard() {
  const FAKE_AUTH = true;

  type AdminTab = "PolarDrive" | "ComingSoon";
  const [activeTab, setActiveTab] = useState<AdminTab>("PolarDrive");
  const [workflowData, setWorkflowData] = useState<WorkflowRow[]>([]);
  const [clients, setClients] = useState<ClientCompany[]>([]);
  const [vehicles, setVehicles] = useState<ClientVehicle[]>([]);
  const [clientConsents, setClientConsents] = useState<ClientConsent[]>([]);
  const [outagePeriods, setOutagePeriods] = useState<OutagePeriod[]>([]);
  const [pdfReports, setPdfReports] = useState<PdfReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [mounted, setMounted] = useState(false);
  const { theme } = useTheme();
  const { t } = useTranslation("common");

  const refreshWorkflowData = async () => {
    try {
      // 🔁 Veicoli
      const resClientVehicles = await fetch(
        `${API_BASE_URL}/api/ClientVehicles`
      );
      const data: ClientVehicleWithCompany[] = await resClientVehicles.json();

      // 🔁 Aziende
      const resCompanies = await fetch(`${API_BASE_URL}/api/clientcompanies`);
      const companies: ClientCompany[] = await resCompanies.json();
      setClients(companies);

      // 🔁 Consensi
      const resConsents = await fetch(`${API_BASE_URL}/api/clientconsents`);
      const consents: ClientConsent[] = await resConsents.json();
      setClientConsents(consents); // 👈 AGGIORNA la tabella Consensi

      // 🔁 AdminMainWorkflow
      setWorkflowData(
        data.map((entry) => ({
          id: entry.id,
          companyVatNumber: entry.clientCompany?.vatNumber ?? "",
          companyName: entry.clientCompany?.name ?? "",
          referentName: entry.clientCompany?.referentName ?? "",
          referentMobile: entry.clientCompany?.referentMobileNumber ?? "",
          referentEmail: entry.clientCompany?.referentEmail ?? "",
          zipFilePath: "",
          uploadDate: entry.firstActivationAt ?? "",
          model: entry.model ?? "",
          fuelType: entry.fuelType ?? "",
          vehicleVIN: entry.vin ?? "",
          color: entry.color ?? "",
          trim: entry.trim ?? "",
          accessToken: "",
          refreshToken: "",
          brand: entry.brand ?? "",
          isVehicleActive: entry.isActive,
          isVehicleFetchingData: entry.isFetching,
          clientOAuthAuthorized: entry.clientOAuthAuthorized ?? false,
        }))
      );

      // 🔁 AdminClientVehiclesTable
      setVehicles(
        data.map((entry) => ({
          id: entry.id,
          clientCompanyId: entry.clientCompany?.id ?? 0,
          vin: entry.vin,
          model: entry.model,
          trim: entry.trim ?? "",
          brand: entry.brand ?? "",
          fuelType: entry.fuelType ?? "",
          color: entry.color ?? "",
          isActive: entry.isActive,
          isFetching: entry.isFetching,
          firstActivationAt: entry.firstActivationAt ?? "",
          lastDeactivationAt: entry.lastDeactivationAt ?? null,
          lastFetchingDataAt: entry.lastFetchingDataAt ?? null,
          clientOAuthAuthorized: entry.clientOAuthAuthorized ?? false,
        }))
      );

      logFrontendEvent(
        "AdminDashboard",
        "DEBUG",
        "Workflow data aggiornati",
        `Veicoli: ${data.length}, Aziende: ${companies.length}`
      );
    } catch (err) {
      console.error("ERROR:", err);
      if (err instanceof Error) {
        logFrontendEvent(
          "AdminDashboard",
          "ERROR",
          "Errore in refreshWorkflowData",
          err.message
        );
      } else {
        logFrontendEvent(
          "AdminDashboard",
          "ERROR",
          "Errore in refreshWorkflowData",
          JSON.stringify(err)
        );
      }
    }
  };

  useEffect(() => {
    refreshWorkflowData();
  }, []);

  useEffect(() => {
    if (!FAKE_AUTH) {
      window.location.href = "/api/auth/signin";
    }
  }, []);

  useEffect(() => {
    setMounted(true);
    logFrontendEvent(
      "AdminDashboard",
      "INFO",
      "Admin dashboard mounted and visible"
    );
  }, []);

  useEffect(() => {
    const fetchAll = async () => {
      try {
        const [clientsRes, vehiclesRes, consentsRes, outagesRes, reportsRes] =
          await Promise.all([
            fetch(`${API_BASE_URL}/api/clientcompanies`),
            fetch(`${API_BASE_URL}/api/clientvehicles`),
            fetch(`${API_BASE_URL}/api/clientconsents`),
            fetch(`${API_BASE_URL}/api/outageperiods`),
            fetch(`${API_BASE_URL}/api/pdfreports`),
          ]);

        const clientsData: ClientCompany[] = await clientsRes.json();
        const vehiclesData: ClientVehicleWithCompany[] =
          await vehiclesRes.json();
        const consentsData: ClientConsent[] = await consentsRes.json();
        const outagesData: OutagePeriod[] = await outagesRes.json();
        const reportsData: PdfReport[] = await reportsRes.json();

        setClients(clientsData);
        setVehicles(
          vehiclesData.map((entry) => ({
            id: entry.id,
            clientCompanyId: entry.clientCompany?.id ?? 0,
            vin: entry.vin,
            model: entry.model,
            trim: entry.trim ?? "",
            brand: entry.brand ?? "",
            fuelType: entry.fuelType ?? "",
            color: entry.color ?? "",
            isActive: entry.isActive,
            isFetching: entry.isFetching,
            firstActivationAt: entry.firstActivationAt ?? "",
            lastDeactivationAt: entry.lastDeactivationAt ?? null,
            lastFetchingDataAt: entry.lastFetchingDataAt ?? null,
            clientOAuthAuthorized: entry.clientOAuthAuthorized ?? false,
          }))
        );
        setClientConsents(consentsData);
        setOutagePeriods(outagesData);
        setPdfReports(reportsData);
        logFrontendEvent(
          "AdminDashboard",
          "INFO",
          "Dati caricati correttamente da tutte le API",
          `Clienti: ${clientsData.length}, Veicoli: ${vehiclesData.length}, Consensi: ${consentsData.length}, Outage: ${outagesData.length}, Report: ${reportsData.length}`
        );
      } catch (err) {
        console.error("API fetch error:", err);
        if (err instanceof Error) {
          logFrontendEvent(
            "AdminDashboard",
            "ERROR",
            "Errore nel fetchAll",
            err.message
          );
        } else {
          logFrontendEvent(
            "AdminDashboard",
            "ERROR",
            "Errore nel fetchAll",
            JSON.stringify(err)
          );
        }
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
                      <AdminMainWorkflow
                        workflowData={workflowData}
                        refreshWorkflowData={refreshWorkflowData}
                      />
                      <AdminClientCompaniesTable
                        clients={clients}
                        t={t}
                        refreshWorkflowData={refreshWorkflowData}
                      />
                      <AdminClientVehiclesTable
                        vehicles={vehicles}
                        t={t}
                        refreshWorkflowData={refreshWorkflowData}
                      />
                      <AdminClientConsents
                        consents={clientConsents}
                        t={t}
                        refreshClientConsents={async () => {
                          const res = await fetch(
                            `${API_BASE_URL}/api/clientconsents`
                          );
                          const updatedClientConsents = await res.json();
                          setClientConsents(updatedClientConsents);
                        }}
                      />
                      <AdminOutagePeriodsTable
                        outages={outagePeriods}
                        t={t}
                        refreshOutagePeriods={async () => {
                          const res = await fetch(
                            `${API_BASE_URL}/api/outageperiods`
                          );
                          const updatedOutagePeriods = await res.json();
                          setOutagePeriods(updatedOutagePeriods);
                          return updatedOutagePeriods;
                        }}
                      />
                      <AdminPdfReports t={t} reports={pdfReports} />
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
