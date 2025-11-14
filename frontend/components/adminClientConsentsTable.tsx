import { TFunction } from "i18next";
import { ClientConsent } from "@/types/clientConsentInterfaces";
import { Download, NotebookPen } from "lucide-react";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { logFrontendEvent } from "@/utils/logger";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import AdminClientConsentAddForm from "./adminClientConsentAddForm";
import AdminLoader from "./adminLoader";

const getConsentTypeColor = (type: string) => {
  switch (type) {
    case "Consent Activation": return "bg-green-100 text-green-700 border-green-500";
    case "Consent Deactivation": return "bg-yellow-100 text-yellow-800 border-yellow-500";
    case "Consent Stop Data Fetching": return "bg-red-100 text-red-700 border-red-500";
    case "Consent Reactivation": return "bg-fuchsia-100 text-fuchsia-700 border-fuchsia-500";
    default: return "bg-gray-100 text-polarNight border-gray-400";
  }
};

export default function AdminClientConsents({ t }: { t: TFunction }) {
  const [consents, setConsents] = useState<ClientConsent[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [selectedConsentForNotes, setSelectedConsentForNotes] = useState<ClientConsent | null>(null);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const pageSize = 5;

  const fetchConsents = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) params.append("search", searchQuery);

      const res = await fetch(`/api/clientconsents?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setConsents(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent("AdminClientConsentsTable", "INFO", "Consents loaded", `Page: ${data.page}, Total: ${data.totalCount}`);
    } catch (err) {
      logFrontendEvent("AdminClientConsentsTable", "ERROR", "Failed to load consents", String(err));
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
        const filenameMatch = contentDisposition.match(/filename[^;=\n]*=["']?([^"';]*)["']?/);
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
      const errorMessage = error instanceof Error ? error.message : "Download failed";
      alert(`${t("admin.downloadError")}: ${errorMessage}`);
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

      setConsents(prev => prev.map(c => c.id === updated.id ? { ...c, notes: updated.notes } : c));
      setSelectedConsentForNotes(null);
      setTimeout(() => fetchConsents(currentPage, query), 200);
    } catch {
      alert(t("admin.notesGenericError"));
    }
  };

  return (
    <div>
      {(loading || isRefreshing) && <AdminLoader />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.clientConsents.tableHeader")}: {totalCount}
        </h1>
        <button
          className={`${showForm ? "bg-red-500 hover:bg-red-600" : "bg-blue-500 hover:bg-blue-600"} text-softWhite px-6 py-2 rounded font-medium transition-colors`}
          onClick={() => setShowForm(!showForm)}
        >
          {showForm ? t("admin.clientConsents.undoAddNewConsent") : t("admin.clientConsents.addNewConsent")}
        </button>
      </div>

      {showForm && (
        <AdminClientConsentAddForm
          t={t}
          onSubmitSuccess={() => setShowForm(false)}
          refreshClientConsents={async () => await fetchConsents(currentPage, query)}
        />
      )}

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              <button onClick={handleRefresh} disabled={isRefreshing} className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 disabled:opacity-50">
                <span className="uppercase text-xs tracking-widest">{t("admin.tableRefreshButton")}</span>
              </button> {t("admin.actions")}
            </th>
            <th className="p-4">{t("admin.clientConsents.consentType")}</th>
            <th className="p-4">{t("admin.clientConsents.uploadDate")}</th>
            <th className="p-4">{t("admin.clientConsents.companyVatNumber")} — {t("admin.clientConsents.vehicleVIN")}</th>
            <th className="p-4">{t("admin.clientConsents.hash")}</th>
          </tr>
        </thead>
        <tbody>
          {consents.map((consent) => (
            <tr key={consent.id} className="border-b border-gray-300 dark:border-gray-600">
              <td className="px-4 py-3">
                <div className="flex items-center space-x-2">
                  {consent.hasZipFile && (
                    <button onClick={() => handleZipDownload(consent.id)} className="p-2 bg-green-500 hover:bg-green-600 text-white rounded" title={t("admin.downloadZip")}>
                      <Download size={16} />
                    </button>
                  )}
                  <button onClick={() => setSelectedConsentForNotes(consent)} className="p-2 bg-blue-500 hover:bg-blue-600 text-white rounded" title={t("admin.openNotesModal")}>
                    <NotebookPen size={16} />
                  </button>
                </div>
              </td>
              <td className="p-4">
                <Chip className={getConsentTypeColor(consent.consentType)}>{consent.consentType}</Chip>
              </td>
              <td className="p-4">{formatDateToDisplay(consent.uploadDate)}</td>
              <td className="p-4">
                <div className="text-sm">
                  <div className="font-mono">{consent.companyVatNumber}</div>
                  <div className="text-gray-500">—</div>
                  <div className="font-mono">{consent.vehicleVIN}</div>
                </div>
              </td>
              <td className="p-4 font-mono text-xs">{consent.consentHash}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex flex-wrap items-center gap-4 mt-4">
        <PaginationControls currentPage={currentPage} totalPages={totalPages} onPrev={() => setCurrentPage(p => Math.max(1, p - 1))} onNext={() => setCurrentPage(p => Math.min(totalPages, p + 1))} />
        <SearchBar query={query} setQuery={setQuery} resetPage={() => setCurrentPage(1)} />
      </div>

      {selectedConsentForNotes && (
        <NotesModal entity={selectedConsentForNotes} isOpen={!!selectedConsentForNotes} title={t("admin.clientConsents.notes.modalTitle")} notesField="notes" onSave={handleNotesUpdate} onClose={() => setSelectedConsentForNotes(null)} t={t} />
      )}
    </div>
  );
}