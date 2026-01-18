import { CircleCheck, CircleX, Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { useEffect, useState } from "react";
import { formatDateToDisplay } from "@/utils/date";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/generic/paginationControls";
import SearchBar from "@/components/generic/searchBar";
import ModalEditTableRows from "@/components/generic/modalEditTableRows";
import AdminEditFormClientVehicle from "@/components/adminEditFormClientVehicle";
import Loader from "@/components/generic/loader";

export default function AdminTableClientVehicles({ t }: { t: TFunction }) {
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
        "AdminTableClientVehicles",
        "INFO",
        "Vehicles loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminTableClientVehicles",
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
      "AdminTableClientVehicles",
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
        "AdminTableClientVehicles",
        "INFO",
        "Vehicle updated successfully",
        `Vehicle VIN: ${updatedVehicle.vin}`
      );
    } catch (err) {
      const details = err instanceof Error ? err.message : String(err);
      logFrontendEvent(
        "AdminTableClientVehicles",
        "ERROR",
        "Error while saving vehicle update",
        details
      );
    }
  };

  return (
    <div className="relative">
      {(loading || isRefreshing) && <Loader local />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.clientVehicle.tableHeader")} ➜ {totalCount}
        </h1>
      </div>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">
              <button
                onClick={handleRefresh}
                disabled={isRefreshing}
                className="px-1 bg-blue-500 text-white rounded text-sm hover:bg-blue-600 disabled:opacity-50"
              >
                <span className="uppercase text-xs tracking-widest">
                  {t("admin.tableRefreshButton")}
                </span>
              </button>
            </th>
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
              className="border-b border-gray-300 dark:border-gray-600"
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

      <div className="flex flex-wrap items-center gap-4 mt-4">
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

      {showEditModal && selectedVehicle && (
        <ModalEditTableRows
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientVehicle.editModal")}
        >
          <AdminEditFormClientVehicle
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
    </div>
  );
}
