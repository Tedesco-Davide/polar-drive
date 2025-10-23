import { useCallback, useEffect, useState } from "react";
import { GetStaticProps } from "next";
import { serverSideTranslations } from "next-i18next/serverSideTranslations";
import { useTheme } from "next-themes";
import { useTranslation } from "next-i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import { FileManager } from "@/types/adminFileManagerTypes";

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
import AdminFileManagerTable from "@/components/adminFileManager";
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
  const [fileManagerJobs, setFileManagerJobs] = useState<FileManager[]>([]);
  const [pdfReports, setPdfReports] = useState<PdfReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [mounted, setMounted] = useState(false);
  const [isRefreshingReports, setIsRefreshingReports] = useState(false);
  const [isRefreshingFileManager, setIsRefreshingFileManager] = useState(false);
  const [isRefreshingOutages, setIsRefreshingOutages] = useState(false);
  const { theme } = useTheme();
  const { t } = useTranslation("common");

  const refreshWorkflowData = async () => {
    try {
      // 🔁 Veicoli
      const resClientVehicles = await fetch(
        `/api/ClientVehicles`
      );
      const data: ClientVehicleWithCompany[] = await resClientVehicles.json();

      // 🔁 Aziende
      const resCompanies = await fetch(`/api/clientcompanies`);
      const companiesData = await resCompanies.json();
      setClients(companiesData);

      // 🔁 Consensi
      const resConsents = await fetch(`/api/clientconsents`);
      const consents: ClientConsent[] = await resConsents.json();
      setClientConsents(consents);

      // 🔁 AdminMainWorkflow
      setWorkflowData(
        data.map((entry) => ({
          id: entry.id,
          companyId: entry.clientCompany?.id ?? 0,
          companyVatNumber: entry.clientCompany?.vatNumber ?? "",
          companyName: entry.clientCompany?.name ?? "",
          referentName: entry.referentName ?? "",
          vehicleMobileNumber: entry.vehicleMobileNumber ?? "",
          referentEmail: entry.referentEmail ?? "",
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
          referentName: entry.referentName ?? "",
          vehicleMobileNumber: entry.vehicleMobileNumber ?? "",
          referentEmail: entry.referentEmail ?? "",
        }))
      );

      logFrontendEvent(
        "AdminDashboard",
        "DEBUG",
        "Workflow data aggiornati",
        "Veicoli: " + data.length + ", Aziende: " + companiesData.length
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

  const refreshPdfReports = useCallback(async (): Promise<PdfReport[]> => {
    try {
      const currentCount = pdfReports.length;

      logFrontendEvent(
        "AdminDashboard",
        "INFO",
        "Starting PDF Reports refresh",
        "Current count: " + currentCount
      );

      const res = await fetch(
        `/api/pdfreports?t=${Date.now()}`,
        {
          headers: {
            "Cache-Control": "no-cache",
            Pragma: "no-cache",
          },
        }
      );

      if (!res.ok) {
        throw new Error("HTTP " + res.status + ": " + res.statusText);
      }

      const updatedPdfReports: PdfReport[] = await res.json();

      setPdfReports([...updatedPdfReports]);

      logFrontendEvent(
        "AdminDashboard",
        "INFO",
        "PDF Reports refreshed successfully",
        currentCount + " → " + updatedPdfReports.length + " reports"
      );

      return updatedPdfReports;
    } catch (err) {
      console.error("Error refreshing PDF reports:", err);
      logFrontendEvent(
        "AdminDashboard",
        "ERROR",
        "Failed to refresh PDF reports",
        err instanceof Error ? err.message : String(err)
      );
      throw err;
    }
  }, [pdfReports.length]);

  const refreshFileManagerJobs = async (): Promise<FileManager[]> => {
    try {
      const res = await fetch(`/api/filemanager`);

      if (!res.ok) {
        throw new Error(
          "FileManager API error: " + res.status + " " + res.statusText
        );
      }

      const contentType = res.headers.get("content-type");
      if (!contentType?.includes("application/json")) {
        throw new Error(
          "FileManager API returned non-JSON content: " + contentType
        );
      }

      const updatedJobs = await res.json();
      setFileManagerJobs(updatedJobs);
      return updatedJobs;
    } catch (error) {
      console.warn("Failed to refresh FileManager jobs:", error);
      logFrontendEvent(
        "AdminDashboard",
        "WARNING",
        "Failed to refresh FileManager jobs",
        error instanceof Error ? error.message : String(error)
      );
      // ✅ Restituisci array vuoto invece di lanciare errore
      return [];
    }
  };

  useEffect(() => {
    refreshWorkflowData();
  }, []);

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
      "Admin dashboard mounted and visible"
    );
  }, []);

  useEffect(() => {
    const fetchAll = async () => {
      try {
        // ✅ Fetch delle API principali (che funzionano)
        const [companiesRes, vehiclesRes, consentsRes, outagesRes, reportsRes] =
          await Promise.all([
            fetch(`/api/clientcompanies`),
            fetch(`/api/clientvehicles`),
            fetch(`/api/clientconsents`),
            fetch(`/api/outageperiods`),
            fetch(`/api/pdfreports`),
          ]);

        const companiesData = await companiesRes.json();
        const vehiclesData: ClientVehicleWithCompany[] =
          await vehiclesRes.json();
        const consentsData: ClientConsent[] = await consentsRes.json();
        const outagesData: OutagePeriod[] = await outagesRes.json();
        const reportsData: PdfReport[] = await reportsRes.json();

        // ✅ Fetch separata per FileManager con gestione errori
        let schedulerData: FileManager[] = [];
        try {
          const schedulerRes = await fetch(`/api/filemanager`);

          if (!schedulerRes.ok) {
            throw new Error(
                "FileManager API error: " + schedulerRes.status + " " + schedulerRes.statusText
            );
          }

          const contentType = schedulerRes.headers.get("content-type");
          if (!contentType?.includes("application/json")) {
            throw new Error(
                "FileManager API returned non-JSON content: " + contentType
            );
          }

          schedulerData = await schedulerRes.json();

          logFrontendEvent(
            "AdminDashboard",
            "INFO",
            "FileManager data loaded successfully",
            "Jobs: " + schedulerData.length
          );
        } catch (fileManagerError) {
          console.warn("FileManager API not available:", fileManagerError);
          logFrontendEvent(
            "AdminDashboard",
            "WARNING",
            "FileManager API not available",
            fileManagerError instanceof Error
              ? fileManagerError.message
              : String(fileManagerError)
          );
          // ✅ Continua senza FileManager data
          schedulerData = [];
        }

        // ✅ Aggiorna tutti gli stati
        setClients(companiesData);

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
            referentName: entry.referentName ?? "",
            vehicleMobileNumber: entry.vehicleMobileNumber ?? "",
            referentEmail: entry.referentEmail ?? "",
          }))
        );
        setClientConsents(consentsData);
        setOutagePeriods(outagesData);
        setPdfReports(reportsData);
        setFileManagerJobs(schedulerData); // ✅ Potrebbe essere array vuoto se API non disponibile

        logFrontendEvent(
          "AdminDashboard",
          "INFO",
          "Dati caricati correttamente da tutte le API",
          "Clienti: " + companiesData.length + ", Veicoli: " + vehiclesData.length + ", Consensi: " + consentsData.length + ", Outage: " + outagesData.length + ", Report: " + reportsData.length + ", Jobs: " + schedulerData.length
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
                            `/api/clientconsents`
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
                            `/api/outageperiods`
                          );
                          const updatedOutagePeriods = await res.json();
                          setOutagePeriods(updatedOutagePeriods);
                          return updatedOutagePeriods;
                        }}
                        isRefreshing={isRefreshingOutages}
                        setIsRefreshing={setIsRefreshingOutages}
                      />
                      <AdminPdfReports
                        t={t}
                        reports={pdfReports}
                        refreshPdfReports={refreshPdfReports}
                        isRefreshing={isRefreshingReports}
                        setIsRefreshing={setIsRefreshingReports}
                      />
                      <AdminFileManagerTable
                        jobs={fileManagerJobs}
                        t={t}
                        refreshJobs={refreshFileManagerJobs}
                        isRefreshing={isRefreshingFileManager}
                        setIsRefreshing={setIsRefreshingFileManager}
                      />
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
