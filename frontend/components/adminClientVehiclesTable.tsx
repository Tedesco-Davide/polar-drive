import { CircleCheck, CircleX, Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useEffect, useState } from "react";
import { formatDateToDisplay } from "@/utils/date";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import EditModal from "@/components/adminEditModal";
import AdminClientVehicleEditForm from "@/components/adminClientVehicleEditForm";

type Props = {
  vehicles: ClientVehicle[];
  refreshWorkflowData: () => Promise<void>;
  t: TFunction;
};

export default function AdminClientVehiclesTable({
  vehicles,
  refreshWorkflowData,
  t,
}: Props) {
  const [vehicleData, setVehicleData] = useState<ClientVehicle[]>([]);
  const { query, setQuery, filteredData } = useSearchFilter<ClientVehicle>(
    vehicleData,
    ["vin", "fuelType", "brand", "model", "trim", "color"]
  );

  useEffect(() => {
    logFrontendEvent(
      "AdminClientVehiclesTable",
      "DEBUG",
      "Search query updated",
      `Query: ${query}`
    );
  }, [query]);

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<ClientVehicle>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminClientVehiclesTable",
      "DEBUG",
      "Pagination interaction",
      `Current page: ${currentPage}`
    );
  }, [currentPage]);

  const [selectedVehicle, setSelectedVehicle] = useState<ClientVehicle | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

  useEffect(() => {
    setVehicleData(vehicles);
    logFrontendEvent(
      "AdminClientVehiclesTable",
      "INFO",
      "Component mounted and vehicle data initialized",
      `Loaded ${vehicles.length} vehicles`
    );
  }, [vehicles]);

  const handleEditClick = (vehicle: ClientVehicle) => {
    setSelectedVehicle(vehicle);
    setShowEditModal(true);
    logFrontendEvent(
      "AdminClientVehiclesTable",
      "INFO",
      "Edit modal opened for vehicle",
      `Vehicle VIN: ${vehicle.vin}`
    );
  };

  return (
    <div>
      <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite mb-12">
        {t("admin.clientVehicle.tableHeader")}
      </h1>
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
          {currentPageData.map((vehicle) => (
            <tr
              key={vehicle.vin}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4">
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  onClick={() => handleEditClick(vehicle)}
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
                  <div className="flex items-center text-green-600">
                    <CircleCheck size={30} />
                  </div>
                ) : (
                  <div className="flex items-center text-red-600">
                    <CircleX size={30} />
                  </div>
                )}
              </td>{" "}
              <td className="p-4">
                {vehicle.isFetching ? (
                  <div className="flex items-center text-green-600">
                    <CircleCheck size={30} />
                  </div>
                ) : (
                  <div className="flex items-center text-red-600">
                    <CircleX size={30} />
                  </div>
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
          onPrev={prevPage}
          onNext={nextPage}
        />
        <SearchBar
          query={query}
          setQuery={setQuery}
          resetPage={() => setCurrentPage(1)}
        />
      </div>

      {showEditModal && selectedVehicle && (
        <EditModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientVehicle.editModal")}
        >
          <AdminClientVehicleEditForm
            vehicle={selectedVehicle}
            onClose={() => setShowEditModal(false)}
            onSave={async (updatedVehicle: ClientVehicle) => {
              try {
                setVehicleData((prev) =>
                  prev.map((v) =>
                    v.id === updatedVehicle.id ? updatedVehicle : v
                  )
                );
                await refreshWorkflowData();
                setShowEditModal(false);
                logFrontendEvent(
                  "AdminClientVehiclesTable",
                  "INFO",
                  "Vehicle updated successfully",
                  `Vehicle VIN: ${updatedVehicle.vin}`
                );
              } catch (err) {
                const details =
                  err instanceof Error ? err.message : String(err);
                logFrontendEvent(
                  "AdminClientVehiclesTable",
                  "ERROR",
                  "Error while saving vehicle update",
                  details
                );
              }
            }}
            refreshWorkflowData={refreshWorkflowData}
            t={t}
          />
        </EditModal>
      )}
    </div>
  );
}
