import { Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientVehicle } from "@/types/vehicleInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useEffect, useState } from "react";
import { formatDateToDisplay } from "@/utils/date";
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
    ["vin", "model", "trim", "color"]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<ClientVehicle>(filteredData, 5);

  const [selectedVehicle, setSelectedVehicle] = useState<ClientVehicle | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

  useEffect(() => {
    setVehicleData(vehicles);
  }, [vehicles]);

  const handleEditClick = (vehicle: ClientVehicle) => {
    setSelectedVehicle(vehicle);
    setShowEditModal(true);
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
              <td className="p-4">{vehicle.model}</td>
              <td className="p-4">{vehicle.trim}</td>
              <td className="p-4">{vehicle.color}</td>
              <td className="p-4 text-2xl">{vehicle.isActive ? "✅" : "🛑"}</td>
              <td className="p-4 text-2xl">
                {vehicle.isFetching ? "✅" : "🛑"}
              </td>
              <td className="p-4">
                {formatDateToDisplay(vehicle.firstActivationAt)}
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
              setVehicleData((prev) =>
                prev.map((v) =>
                  v.id === updatedVehicle.id ? updatedVehicle : v
                )
              );
              await refreshWorkflowData();
              setShowEditModal(false);
            }}
            refreshWorkflowData={refreshWorkflowData}
            t={t}
          />
        </EditModal>
      )}
    </div>
  );
}
