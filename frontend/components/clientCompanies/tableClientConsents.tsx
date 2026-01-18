import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { Download, NotebookPen, FileSignature, ChevronDown, ChevronUp } from "lucide-react";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { usePreventUnload } from "@/hooks/usePreventUnload";
import { logFrontendEvent } from "@/utils/logger";
import ModalEditNotes from "@/components/generic/modalEditNotes";
import SearchBar from "@/components/generic/searchBar";
import Chip from "@/components/generic/chip";
import AddFormClientConsent from "./addFormClientConsent";
import Loader from "@/components/generic/loader";
import PaginationControls from "@/components/generic/paginationControls";

const getConsentTypeColor = (type: string) => {
  switch (type) {
    case "Consent Activation":
      return "bg-green-100 text-green-700 border-green-500";
    case "Consent Deactivation":
      return "bg-yellow-100 text-yellow-800 border-yellow-500";
    case "Consent Stop Data Fetching":
      return "bg-red-100 text-red-700 border-red-500";
    case "Consent Reactivation":
      return "bg-fuchsia-100 text-fuchsia-700 border-fuchsia-500";
    default:
      return "bg-gray-100 text-polarNight border-gray-400";
  }
};

export default function TableClientConsents({ t }: { t: TFunction }) {
  const [consents, setConsents] = useState<ClientConsent[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [selectedConsentForNotes, setSelectedConsentForNotes] =
    useState<ClientConsent | null>(null);
  const [downloadingZipId, setDownloadingZipId] = useState<number | null>(null);

  usePreventUnload(downloadingZipId !== null);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status">("id");
  const pageSize = 5;

  const fetchConsents = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        params.append("searchType", searchType);
      }
      const res = await fetch(`/api/clientconsents?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setConsents(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "ClientConsentsTable",
        "INFO",
        "Consents loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "ClientConsentsTable",
        "ERROR",
        "Failed to load consents",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchConsents(currentPage, query);
  }, [currentPage, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchConsents(currentPage, query);
    setIsRefreshing(false);
  };

  const handleZipDownload = async (consentId: number) => {
    setDownloadingZipId(consentId);
    try {
      const response = await fetch(`/api/clientconsents/${consentId}/download`);
      const contentType = response.headers.get("content-type");

      if (contentType && contentType.includes("application/json")) {
        const result = await response.json();
        alert(result.message || t("admin.noFileAvailable"));
        return;
      }

      if (!response.ok) throw new Error("HTTP " + response.status);

      const contentDisposition = response.headers.get("content-disposition");
      let filename = `consent_${consentId}.zip`;
      if (contentDisposition) {
        const filenameMatch = contentDisposition.match(
          /filename[^;=\n]*=["']?([^"';]*)["']?/
        );
        if (filenameMatch && filenameMatch[1]) filename = filenameMatch[1];
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Download failed";
      alert(`${t("admin.downloadError")}: ${errorMessage}`);
    } finally {
      setDownloadingZipId(null);
    }
  };

  const handleNotesUpdate = async (updated: ClientConsent) => {
    try {
      const response = await fetch(`/api/clientconsents/${updated.id}/notes`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ notes: updated.notes }),
      });

      if (!response.ok) throw new Error("HTTP " + response.status);

      setConsents((prev) =>
        prev.map((c) =>
          c.id === updated.id ? { ...c, notes: updated.notes } : c
        )
      );
      setSelectedConsentForNotes(null);
      setTimeout(() => fetchConsents(currentPage, query), 200);
    } catch {
      alert(t("admin.notesGenericError"));
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut", delay: 0.1 }}
      className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
    >
      {(loading || isRefreshing || downloadingZipId !== null) && <Loader local />}

      <div className="bg-gradient-to-r from-coldIndigo/10 via-purple-500/5 to-glacierBlue/10 dark:from-coldIndigo/20 dark:via-purple-900/10 dark:to-glacierBlue/20 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div className="flex items-center space-x-4">
            <button
              onClick={handleRefresh}
              disabled={isRefreshing}
              className="p-3 bg-blue-500 hover:bg-blue-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md disabled:opacity-50"
            >
              {t("admin.tableRefreshButton")}
            </button>
            <button
              onClick={() => setShowForm(!showForm)}
              className={`p-3 ${
                showForm
                  ? "bg-red-500 hover:bg-red-600"
                  : "bg-blue-500 hover:bg-blue-600"
              } text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md flex items-center gap-2`}
            >
              {showForm ? (
                <>
                  <ChevronUp size={18} />
                  {t("admin.clientConsents.hideForm")}
                </>
              ) : (
                <>
                  <ChevronDown size={18} />
                  {t("admin.clientConsents.addNewConsent")}
                </>
              )}
            </button>
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <FileSignature size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.clientConsents.tableHeader")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {totalCount} {t("admin.totals")}
              </p>
            </div>
          </div>
        </div>
      </div>

      <AnimatePresence>
        {showForm && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.3, ease: "easeInOut" }}
            className="overflow-hidden border-b border-gray-200 dark:border-gray-700"
          >
            <div className="p-6 bg-gray-50 dark:bg-gray-800/50">
              <AddFormClientConsent
                t={t}
                onSubmitSuccess={() => setShowForm(false)}
                refreshClientConsents={async () =>
                  await fetchConsents(currentPage, query)
                }
              />
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <div className="p-6 overflow-x-auto">
        <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-4 rounded-tl-lg">{t("admin.actions")}</th>
              <th className="p-4">{t("admin.clientConsents.consentType")}</th>
              <th className="p-4">{t("admin.clientConsents.uploadDate")}</th>
              <th className="p-4">
                {t("admin.clientConsents.companyVatNumber")} —{" "}
                {t("admin.clientConsents.vehicleVIN")}
              </th>
              <th className="p-4 rounded-tr-lg">{t("admin.clientConsents.hash")}</th>
            </tr>
          </thead>
          <tbody>
            {consents.map((consent) => (
              <tr
                key={consent.id}
                className="border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                <td className="p-4">
                  <div className="flex items-center space-x-2">
                    {consent.hasZipFile && (
                      <button
                        onClick={() => handleZipDownload(consent.id)}
                        className="p-2 bg-green-500 hover:bg-green-600 text-white rounded-lg transition-colors shadow-sm hover:shadow-md disabled:opacity-50 disabled:cursor-not-allowed"
                        title={t("admin.downloadZip")}
                        disabled={downloadingZipId === consent.id}
                      >
                        <Download size={16} />
                      </button>
                    )}
                    <button
                      onClick={() => setSelectedConsentForNotes(consent)}
                      className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded-lg transition-colors shadow-sm hover:shadow-md"
                      title={t("admin.openNotesModal")}
                    >
                      <NotebookPen size={16} />
                    </button>
                  </div>
                </td>
                <td className="p-4">
                  <Chip className={getConsentTypeColor(consent.consentType)}>
                    {consent.consentType}
                  </Chip>
                </td>
                <td className="p-4">
                  <div className="font-medium">
                    {formatDateToDisplay(consent.uploadDate)}
                  </div>
                  <div className="text-xs text-gray-400 mt-1">
                    ID {consent.id}
                  </div>
                </td>
                <td className="p-4">
                  <div className="text-sm">
                    <div className="font-mono">{consent.companyVatNumber}</div>
                    <div className="text-gray-400">—</div>
                    <div className="font-mono">{consent.vehicleVIN}</div>
                  </div>
                </td>
                <td className="p-4 font-mono text-xs text-gray-600 dark:text-gray-400">
                  {consent.consentHash}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="px-6 pb-6">
        <div className="flex flex-wrap items-center gap-4">
          <PaginationControls
            currentPage={currentPage}
            totalPages={totalPages}
            onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
            onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
          />
          <SearchBar
            query={query}
            setQuery={setQuery}
            resetPage={() => setCurrentPage(1)}
            searchMode="id-or-status"
            externalSearchType={searchType}
            onSearchTypeChange={(type) => {
              if (type === "id" || type === "status") {
                setSearchType(type);
              }
            }}
            statusLabel={t("admin.clientConsents.consentType")}
            selectPlaceholder={t("admin.searchButton.selectConsent")}
            availableStatuses={[
              "Consent Activation",
              "Consent Deactivation",
              "Consent Stop Data Fetching",
              "Consent Reactivation",
            ]}
          />
        </div>
      </div>

      {selectedConsentForNotes && (
        <ModalEditNotes
          entity={selectedConsentForNotes}
          isOpen={!!selectedConsentForNotes}
          title={t("admin.clientConsents.notes.modalTitle")}
          notesField="notes"
          onSave={handleNotesUpdate}
          onClose={() => setSelectedConsentForNotes(null)}
          t={t}
        />
      )}
    </motion.div>
  );
}
