import { CircleCheck, CircleX, Pencil, Car } from "lucide-react";
import { TFunction } from "i18next";
import { motion } from "framer-motion";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { useEffect, useState } from "react";
import { formatDateToDisplay } from "@/utils/date";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/generic/paginationControls";
import SearchBar from "@/components/generic/searchBar";
import ModalEditTableRows from "@/components/generic/modalEditTableRows";
import EditFormClientVehicle from "@/components/vehicleWorkflow/editFormClientVehicle";
import Loader from "@/components/generic/loader";
import Chip from "@/components/generic/chip";

export default function TableClientVehicles({ t }: { t: TFunction }) {
  const [vehicleData, setVehicleData] = useState<ClientVehicle[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [selectedVehicle, setSelectedVehicle] = useState<ClientVehicle | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status">("id");
  const pageSize = 5;

  const fetchVehicles = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        const type = searchType === "id" ? "vin" : "company";
        params.append("searchType", type);
      }

      const res = await fetch(`/api/clientvehicles?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setVehicleData(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "TableClientVehicles",
        "INFO",
        "Vehicles loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "TableClientVehicles",
        "ERROR",
        "Failed to load vehicles",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchVehicles(currentPage, query);
  }, [currentPage, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchVehicles(currentPage, query);
    setIsRefreshing(false);
  };

  const handleEditClick = (vehicle: ClientVehicle) => {
    setSelectedVehicle(vehicle);
    setShowEditModal(true);
    logFrontendEvent(
      "TableClientVehicles",
      "INFO",
      "Edit modal opened for vehicle",
      `Vehicle VIN: ${vehicle.vin}`
    );
  };

  const handleSave = async (updatedVehicle: ClientVehicle) => {
    try {
      setVehicleData((prev) =>
        prev.map((v) => (v.id === updatedVehicle.id ? updatedVehicle : v))
      );
      await fetchVehicles(currentPage, query);
      setShowEditModal(false);
      logFrontendEvent(
        "TableClientVehicles",
        "INFO",
        "Vehicle updated successfully",
        `Vehicle VIN: ${updatedVehicle.vin}`
      );
    } catch (err) {
      const details = err instanceof Error ? err.message : String(err);
      logFrontendEvent(
        "TableClientVehicles",
        "ERROR",
        "Error while saving vehicle update",
        details
      );
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut", delay: 0.1 }}
      className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
    >
      {(loading || isRefreshing) && <Loader local />}

      {/* Header con gradiente */}
      <div className="bg-gradient-to-r from-coldIndigo/10 via-purple-500/5 to-glacierBlue/10 dark:from-coldIndigo/20 dark:via-purple-900/10 dark:to-glacierBlue/20 px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <div className="flex flex-col sm:flex-row sm:items-center gap-4">
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-3">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing}
                className="p-3 bg-blue-500 hover:bg-blue-600 text-white rounded-lg text-sm font-medium transition-all duration-200 shadow-sm hover:shadow-md disabled:opacity-50"
              >
                {t("admin.tableRefreshButton")}
              </button>
            </div>
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <Car size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.clientVehicle.tableHeader")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {totalCount} {t("admin.totals")}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Table Content */}
      <div className="p-6">
        {/* Vista MOBILE: Card Layout */}
        <div className="lg:hidden space-y-4">
          {vehicleData.map((vehicle) => (
            <div
              key={vehicle.vin}
              className="bg-softWhite dark:bg-gray-800 rounded-lg shadow-md border border-gray-300 dark:border-gray-600 overflow-hidden"
            >
              {/* Header Card con info veicolo */}
              <div className="p-3 bg-gray-100 dark:bg-gray-700">
                <div className="flex items-start justify-between gap-2 mb-1">
                  <span className="font-bold text-base text-polarNight dark:text-softWhite">
                    {vehicle.brand} {vehicle.model}
                  </span>
                  <div className="flex gap-1">
                    <Chip
                      className={`text-[10px] whitespace-nowrap px-2 py-1 ${
                        vehicle.isActive
                          ? "bg-green-100 text-green-700 border-green-500"
                          : "bg-red-100 text-red-700 border-red-500"
                      }`}
                    >
                      {vehicle.isActive ? "Active" : "Inactive"}
                    </Chip>
                  </div>
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400 font-mono break-all">
                  VIN: {vehicle.vin}
                </div>
              </div>

              {/* Dettagli Veicolo */}
              <div className="p-3 text-sm border-t border-gray-200 dark:border-gray-600">
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <span className="text-gray-500 dark:text-gray-400 text-xs">
                      {t("admin.clientVehicle.fuelType")}:
                    </span>
                    <span className="ml-1 font-medium text-polarNight dark:text-softWhite text-xs">
                      {vehicle.fuelType}
                    </span>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400 text-xs">
                      {t("admin.clientVehicle.trim")}:
                    </span>
                    <span className="ml-1 font-medium text-polarNight dark:text-softWhite text-xs">
                      {vehicle.trim || "-"}
                    </span>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400 text-xs">
                      {t("admin.clientVehicle.color")}:
                    </span>
                    <span className="ml-1 font-medium text-polarNight dark:text-softWhite text-xs">
                      {vehicle.color || "-"}
                    </span>
                  </div>
                  <div>
                    <span className="text-gray-500 dark:text-gray-400 text-xs">
                      {t("admin.clientVehicle.isFetching")}:
                    </span>
                    <span className="ml-1">
                      {vehicle.isFetching ? (
                        <CircleCheck size={16} className="inline text-green-600" />
                      ) : (
                        <CircleX size={16} className="inline text-red-600" />
                      )}
                    </span>
                  </div>
                </div>
              </div>

              {/* Date */}
              <div className="p-3 bg-gray-50 dark:bg-gray-750 border-t border-gray-200 dark:border-gray-600">
                <div className="grid grid-cols-1 gap-1 text-xs">
                  <div className="flex justify-between">
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.clientVehicle.firstActivationAt")}:
                    </span>
                    <span className="font-medium text-polarNight dark:text-softWhite">
                      {vehicle.firstActivationAt
                        ? formatDateToDisplay(vehicle.firstActivationAt)
                        : t("admin.basicPlaceholder")}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.clientVehicle.lastDeactivationAt")}:
                    </span>
                    <span className="font-medium text-polarNight dark:text-softWhite">
                      {vehicle.lastDeactivationAt
                        ? formatDateToDisplay(vehicle.lastDeactivationAt)
                        : t("admin.basicPlaceholder")}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500 dark:text-gray-400">
                      {t("admin.clientVehicle.lastFetchingDataAt")}:
                    </span>
                    <span className="font-medium text-polarNight dark:text-softWhite">
                      {vehicle.lastFetchingDataAt
                        ? formatDateToDisplay(vehicle.lastFetchingDataAt)
                        : t("admin.basicPlaceholder")}
                    </span>
                  </div>
                </div>
              </div>

              {/* Bottone Edit */}
              <div className="p-3 border-t border-gray-200 dark:border-gray-600">
                <button
                  onClick={() => handleEditClick(vehicle)}
                  className="w-full flex items-center justify-center gap-2 p-2 bg-blue-500 hover:bg-blue-600 text-white rounded-lg"
                >
                  <Pencil size={16} />
                  <span className="text-sm font-medium">{t("admin.edit")}</span>
                </button>
              </div>
            </div>
          ))}
        </div>

        {/* Vista DESKTOP: Table Layout */}
        <div className="hidden lg:block overflow-x-auto">
          <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
            <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
              <tr>
                <th className="p-4">{t("admin.actions")}</th>
                <th className="p-4">{t("admin.vehicleVIN")}</th>
                <th className="p-4">{t("admin.clientVehicle.fuelType")}</th>
                <th className="p-4">{t("admin.clientVehicle.brand")}</th>
                <th className="p-4">{t("admin.clientVehicle.model")}</th>
                <th className="p-4">{t("admin.clientVehicle.trim")}</th>
                <th className="p-4">{t("admin.clientVehicle.color")}</th>
                <th className="p-4">{t("admin.clientVehicle.isActive")}</th>
                <th className="p-4">{t("admin.clientVehicle.isFetching")}</th>
                <th className="p-4">
                  {t("admin.clientVehicle.firstActivationAt")}
                </th>
                <th className="p-4">
                  {t("admin.clientVehicle.lastDeactivationAt")}
                </th>
                <th className="p-4">
                  {t("admin.clientVehicle.lastFetchingDataAt")}
                </th>
              </tr>
            </thead>
            <tbody>
              {vehicleData.map((vehicle) => (
                <tr
                  key={vehicle.vin}
                  className="border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
                >
                  <td className="p-4">
                    <button
                      onClick={() => handleEditClick(vehicle)}
                      className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                      title={t("admin.edit")}
                    >
                      <Pencil size={16} />
                    </button>
                  </td>
                  <td className="p-4">{vehicle.vin}</td>
                  <td className="p-4">{vehicle.fuelType}</td>
                  <td className="p-4">{vehicle.brand}</td>
                  <td className="p-4">{vehicle.model}</td>
                  <td className="p-4">{vehicle.trim}</td>
                  <td className="p-4">{vehicle.color}</td>
                  <td className="p-4">
                    {vehicle.isActive ? (
                      <CircleCheck size={30} className="text-green-600" />
                    ) : (
                      <CircleX size={30} className="text-red-600" />
                    )}
                  </td>
                  <td className="p-4">
                    {vehicle.isFetching ? (
                      <CircleCheck size={30} className="text-green-600" />
                    ) : (
                      <CircleX size={30} className="text-red-600" />
                    )}
                  </td>
                  <td className="p-4">
                    {vehicle.firstActivationAt
                      ? formatDateToDisplay(vehicle.firstActivationAt)
                      : t("admin.basicPlaceholder")}
                  </td>
                  <td className="p-4">
                    {vehicle.lastDeactivationAt
                      ? formatDateToDisplay(vehicle.lastDeactivationAt)
                      : t("admin.basicPlaceholder")}
                  </td>
                  <td className="p-4">
                    {vehicle.lastFetchingDataAt
                      ? formatDateToDisplay(vehicle.lastFetchingDataAt)
                      : t("admin.basicPlaceholder")}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Controlli Paginazione e Ricerca */}
        <div className="flex flex-col lg:flex-row items-stretch lg:items-center gap-3 lg:gap-4 mt-6 w-full">
          <div className="order-2 lg:order-1 shrink-0">
            <PaginationControls
              currentPage={currentPage}
              totalPages={totalPages}
              onPrev={() => setCurrentPage((p) => Math.max(1, p - 1))}
              onNext={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
            />
          </div>
          <div className="order-1 lg:order-2 flex-1 min-w-0">
            <SearchBar
              query={query}
              setQuery={setQuery}
              resetPage={() => setCurrentPage(1)}
              searchMode="vin-or-company"
              externalSearchType={searchType}
              onSearchTypeChange={(type) => {
                if (type === "id" || type === "status") {
                  setSearchType(type);
                }
              }}
              vatLabel={t("admin.vehicleVIN")}
              companyLabel={t("admin.clientCompany.name")}
            />
          </div>
        </div>
      </div>

      {showEditModal && selectedVehicle && (
        <ModalEditTableRows
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientVehicle.editModal")}
        >
          <EditFormClientVehicle
            vehicle={selectedVehicle}
            onClose={() => setShowEditModal(false)}
            onSave={handleSave}
            refreshWorkflowData={async () =>
              await fetchVehicles(currentPage, query)
            }
            t={t}
          />
        </ModalEditTableRows>
      )}
    </motion.div>
  );
}