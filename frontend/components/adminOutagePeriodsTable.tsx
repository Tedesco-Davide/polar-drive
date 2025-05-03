import { TFunction } from "i18next";
import { OutagePeriod } from "@/types/outagePeriodInterfaces";
import { FileArchive, HardDriveUpload, NotebookPen } from "lucide-react";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useState, useEffect } from "react";
import { parseISO, isValid, isAfter } from "date-fns";
import { OutageFormData } from "@/types/outagePeriodTypes";
import { formatDateToDisplay } from "@/utils/date";
import AdminOutagePeriodsAddForm from "./adminOutagePeriodsAddForm";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import Chip from "@/components/chip";
import NotesModal from "./notesModal";

const getOutageStatus = (start: string, end?: string) => {
  return end ? "OUTAGE-RESOLVED" : "OUTAGE-ONGOING";
};

const getStatusColor = (status: string) => {
  switch (status) {
    case "OUTAGE-ONGOING":
      return "bg-red-100 text-red-700 border-red-500";
    case "OUTAGE-RESOLVED":
      return "bg-green-100 text-green-700 border-green-500";
    default:
      return "bg-gray-100 text-polarNight border-gray-400";
  }
};

const getOutageDuration = (start: string, end?: string): string => {
  const startDate = parseISO(start);
  const endDate = end ? parseISO(end) : new Date();
  const diffMs = endDate.getTime() - startDate.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const days = Math.floor(diffMins / 1440);
  const hours = Math.floor((diffMins % 1440) / 60);
  const minutes = diffMins % 60;
  return `${days > 0 ? days + "g " : ""}${hours > 0 ? hours + "h " : ""}${
    days === 0 && hours === 0 ? minutes + "m" : ""
  }`.trim();
};

type Props = {
  t: TFunction;
  outages: OutagePeriod[];
};

export default function AdminOutagePeriodsTable({ t, outages }: Props) {
  const [localOutages, setLocalOutages] = useState<OutagePeriod[]>([]);
  const [showForm, setShowForm] = useState(false);

  const [formData, setFormData] = useState<OutageFormData>({
    autoDetected: false,
    status: "",
    outageType: "",
    outageStart: "",
    outageEnd: undefined,
    companyVatNumber: "",
    vin: "",
    zipFilePath: null,
  });

  useEffect(() => {
    setLocalOutages(outages);
  }, [outages]);

  const [selectedOutageForNotes, setSelectedOutageForNotes] =
    useState<OutagePeriod | null>(null);

  const { query, setQuery, filteredData } = useSearchFilter<OutagePeriod>(
    localOutages,
    ["clientCompanyId", "teslaVehicleId", "outageType", "notes"]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<OutagePeriod>(filteredData, 5);

  const handleZipUpload = (index: number, file: File) => {
    if (!file || !file.name.endsWith(".zip")) {
      alert(t("admin.validation.invalidZipType"));
      return;
    }

    const fakeUrl = URL.createObjectURL(file);
    const updated = [...localOutages];
    updated[index].zipFilePath = fakeUrl;
    setLocalOutages(updated);
  };

  const handleSubmit = () => {
    if (!formData.outageStart) {
      alert(t("admin.outagePeriods.validation.startDateRequired"));
      return;
    }

    const outageStartDate = parseISO(formData.outageStart);
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    if (!isValid(outageStartDate) || isAfter(outageStartDate, today)) {
      alert(t("admin.outagePeriods.validation.startDateInFuture"));
      return;
    }

    if (formData.status === "") {
      alert(t("admin.outagePeriods.validation.statusRequired"));
      return;
    }

    if (formData.outageType === "") {
      alert(t("admin.outagePeriods.validation.outageTypeRequired"));
      return;
    }

    // Se VIN e PIVA sono entrambi vuoti, è un Outage Fleet API generico
    const isGenericOutage =
      (!formData.vin || formData.vin.trim() === "") &&
      (!formData.companyVatNumber || formData.companyVatNumber.trim() === "");

    const newOutage: OutagePeriod = {
      id: Math.floor(Math.random() * 100000),
      teslaVehicleId: isGenericOutage ? 0 : 9999,
      clientCompanyId: isGenericOutage ? 0 : 9999,
      autoDetected: formData.autoDetected,
      outageType: formData.outageType,
      createdAt: formData.outageStart,
      outageStart: formData.outageStart,
      outageEnd: formData.outageEnd,
      vin: formData.vin || "",
      companyVatNumber: formData.companyVatNumber || "",
      zipFilePath: formData.zipFilePath
        ? URL.createObjectURL(formData.zipFilePath)
        : undefined,
      notes: "",
    };

    setLocalOutages((prev) => [newOutage, ...prev]);
    setCurrentPage(1);

    // ✅ Reset corretto come da stato iniziale
    setFormData({
      autoDetected: false,
      status: "",
      outageType: "",
      outageStart: "",
      outageEnd: undefined,
      companyVatNumber: "",
      vin: "",
      zipFilePath: null,
    });

    setShowForm(false);
    alert(t("admin.outagePeriods.successAddNewOutage"));
  };

  return (
    <div>
      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.outagePeriods.tableHeader")}
        </h1>
        <button
          className={`${
            showForm
              ? "bg-dataRed hover:bg-red-600"
              : "bg-blue-500 hover:bg-blue-600"
          } text-softWhite px-6 py-2 rounded`}
          onClick={() => setShowForm(!showForm)}
        >
          {showForm
            ? t("admin.outagePeriods.undoAddNewOutage")
            : t("admin.outagePeriods.addNewOutage")}
        </button>
      </div>

      {showForm && (
        <AdminOutagePeriodsAddForm
          formData={formData}
          setFormData={setFormData}
          onSubmit={handleSubmit}
          t={t}
        />
      )}

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
            <th className="p-4">{t("admin.outagePeriods.autoDetected")}</th>
            <th className="p-4">{t("admin.outagePeriods.status")}</th>
            <th className="p-4">{t("admin.outagePeriods.outageType")}</th>
            <th className="p-4">
              {t("admin.outagePeriods.outageStart")} -{" "}
              {t("admin.outagePeriods.outageEnd")}
            </th>
            <th className="p-4">{t("admin.outagePeriods.duration")}</th>
            <th className="p-4">
              {t("admin.outagePeriods.companyVatNumber")}{" "}
              {t("admin.basicPlaceholder")}{" "}
              {t("admin.outagePeriods.teslaVehicleVIN")}
            </th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((outage, index) => {
            const status = getOutageStatus(
              outage.outageStart,
              outage.outageEnd
            );
            return (
              <tr
                key={index}
                className="border-b border-gray-300 dark:border-gray-600"
              >
                <td className="p-4 space-x-2 inline-flex items-center">
                  {outage.zipFilePath ? (
                    <button
                      className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                      title={t("admin.outagePeriods.viewZipOutage")}
                      onClick={() => window.open(outage.zipFilePath, "_blank")}
                    >
                      <FileArchive size={16} />
                    </button>
                  ) : (
                    <label
                      title={t("admin.outagePeriods.uploadZipOutage")}
                      className="cursor-pointer p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    >
                      <HardDriveUpload size={16} />
                      <input
                        type="file"
                        accept=".zip"
                        className="hidden"
                        onChange={(e) =>
                          e.target.files &&
                          handleZipUpload(
                            localOutages.findIndex((o) => o.id === outage.id),
                            e.target.files[0]
                          )
                        }
                      />
                    </label>
                  )}
                  <button
                    className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                    title={t("admin.openNotesModal")}
                    onClick={() => setSelectedOutageForNotes(outage)}
                  >
                    <NotebookPen size={16} />
                  </button>
                </td>
                <td className="p-4 text-2xl">
                  {outage.autoDetected ? "✅" : "🛑"}
                </td>
                <td className="p-4">
                  <Chip className={getStatusColor(status)}>{status}</Chip>
                </td>
                <td className="p-4">{outage.outageType}</td>
                <td className="p-4">
                  {formatDateToDisplay(outage.outageStart)} -{" "}
                  {outage.outageEnd ? (
                    formatDateToDisplay(outage.outageEnd)
                  ) : (
                    <Chip className={getStatusColor("OUTAGE-ONGOING")}>
                      OUTAGE-ONGOING
                    </Chip>
                  )}
                </td>
                <td className="p-4">
                  {outage.outageEnd ? (
                    getOutageDuration(outage.outageStart, outage.outageEnd)
                  ) : (
                    <Chip className="bg-red-100 text-red-700 border-red-500">
                      OUTAGE-ONGOING
                    </Chip>
                  )}
                </td>
                <td className="p-4">
                  {outage.companyVatNumber} {t("admin.basicPlaceholder")}{" "}
                  {outage.vin}
                </td>
              </tr>
            );
          })}
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

      {selectedOutageForNotes && (
        <NotesModal
          entity={selectedOutageForNotes}
          isOpen={!!selectedOutageForNotes}
          title={t("admin.outagePeriods.notes.modalTitle")}
          notesField="notes"
          onSave={(updated) => {
            setLocalOutages((prev) =>
              prev.map((o) =>
                o.id === updated.id ? { ...o, notes: updated.notes } : o
              )
            );
            setSelectedOutageForNotes(null);
          }}
          onClose={() => setSelectedOutageForNotes(null)}
          t={t}
        />
      )}
    </div>
  );
}
