import { TFunction } from "i18next";
import { PdfReport } from "@/types/reportInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { formatDateToDisplay } from "@/utils/date";
import { useState, useEffect } from "react";
import { FileText, NotebookPen } from "lucide-react";
import NotesModal from "@/components/notesModal";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";

type Props = {
  t: TFunction;
  reports: PdfReport[];
};

export default function AdminTeslaVehiclesReport({ t, reports }: Props) {
  const [localReports, setLocalReports] = useState<PdfReport[]>([]);
  const [selectedReportForNotes, setSelectedReportForNotes] =
    useState<PdfReport | null>(null);

  useEffect(() => {
    setLocalReports(reports);
  }, [reports]);

  const { query, setQuery, filteredData } = useSearchFilter<PdfReport>(
    localReports,
    [
      "companyVatNumber",
      "companyName",
      "vehicleVin",
      "vehicleDisplayName",
      "reportPeriodStart",
      "reportPeriodEnd",
    ]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<PdfReport>(filteredData, 5);

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.teslaReports.tableHeader")}
        </h1>
      </div>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
            <th className="p-4">{t("admin.teslaReports.reportPeriod")}</th>
            <th className="p-4">
              {t("admin.teslaReports.clientCompanyVATName")}
            </th>
            <th className="p-4">{t("admin.teslaReports.vehicleVinDisplay")}</th>
            <th className="p-4">{t("admin.teslaReports.generatedAt")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((report, index) => (
            <tr
              key={index}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2 inline-flex">
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  title={t("admin.teslaReports.downloadSinglePdf")}
                  onClick={() => window.open(report.pdfFilePath, "_blank")}
                >
                  <FileText size={16} />
                </button>
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  title={t("admin.openNotesModal")}
                  onClick={() => setSelectedReportForNotes(report)}
                >
                  <NotebookPen size={16} />
                </button>
                {selectedReportForNotes && (
                  <NotesModal
                    entity={selectedReportForNotes}
                    isOpen={!!selectedReportForNotes}
                    title={t("admin.teslaReports.notesModalTitle")}
                    notesField="notes"
                    onSave={(updated) => {
                      setLocalReports((prev) =>
                        prev.map((r) =>
                          r.pdfFilePath === updated.pdfFilePath
                            ? { ...r, notes: updated.notes }
                            : r
                        )
                      );
                      setSelectedReportForNotes(null);
                    }}
                    onClose={() => setSelectedReportForNotes(null)}
                    t={t}
                  />
                )}
              </td>
              <td className="p-4">
                {formatDateToDisplay(report.reportPeriodStart)} -{" "}
                {formatDateToDisplay(report.reportPeriodEnd)}
              </td>
              <td className="p-4">
                {report.companyVatNumber} - {report.companyName}
              </td>
              <td className="p-4">
                {report.vehicleVin} - {report.vehicleDisplayName}
              </td>
              <td className="p-4">{formatDateToDisplay(report.generatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex flex-wrap items-center gap-4 mt-4">
        <PaginationControls
          currentPage={currentPage}
          totalPages={totalPages}
          onPrev={prevPage}
          onNext={nextPage}
        />
        <SearchBar
          query={query}
          setQuery={setQuery}
          resetPage={() => setCurrentPage(1)}
        />
      </div>
    </div>
  );
}
