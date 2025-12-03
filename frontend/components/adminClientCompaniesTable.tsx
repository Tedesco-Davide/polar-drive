import { useState, useEffect } from "react";
import { Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import EditModal from "@/components/adminEditModal";
import AdminClientCompanyEditForm from "@/components/adminClientCompanyEditForm";
import AdminLoader from "@/components/adminLoader";

export default function AdminClientCompaniesTable({ t }: { t: TFunction }) {
  const [clientData, setClientData] = useState<ClientCompany[]>([]);
  const [loading, setLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [selectedClient, setSelectedClient] = useState<ClientCompany | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [query, setQuery] = useState("");
  const [searchType, setSearchType] = useState<"id" | "status">("id");
  const pageSize = 5;

  const fetchClients = async (page: number, searchQuery: string = "") => {
    try {
      setLoading(true);
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      });
      if (searchQuery) {
        params.append("search", searchQuery);
        const type = searchType === "id" ? "vat" : "name";
        params.append("searchType", type);
      }

      const res = await fetch(`/api/clientcompanies?${params}`);
      if (!res.ok) throw new Error("HTTP " + res.status);

      const data = await res.json();
      setClientData(data.data);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setCurrentPage(data.page);

      logFrontendEvent(
        "AdminClientCompaniesTable",
        "INFO",
        "Clients loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "AdminClientCompaniesTable",
        "ERROR",
        "Failed to load clients",
        String(err)
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchClients(currentPage, query);
  }, [currentPage, query]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await fetchClients(currentPage, query);
    setIsRefreshing(false);
  };

  const handleEditClick = (client: ClientCompany) => {
    setSelectedClient(client);
    setShowEditModal(true);
    logFrontendEvent(
      "AdminClientCompaniesTable",
      "INFO",
      "Edit modal opened for client",
      `ClientId: ${client.id}, VAT: ${client.vatNumber}`
    );
  };

  const handleSave = (updatedClient: ClientCompany) => {
    setClientData((prev) =>
      prev.map((c) =>
        c.id === updatedClient.id &&
        c.correspondingVehicleId === updatedClient.correspondingVehicleId
          ? updatedClient
          : c
      )
    );
    setShowEditModal(false);
    setTimeout(() => fetchClients(currentPage, query), 200);
  };

  return (
    <div className="relative">
      {(loading || isRefreshing) && <AdminLoader local />}

      <div className="flex items-center mb-12 space-x-3">
        <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite">
          {t("admin.clientCompany.tableHeader")} ➜ {totalCount}
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
              </button>{" "}
              {t("admin.actions")}
            </th>
            <th className="p-4">{t("admin.clientCompany.vatNumber")}</th>
            <th className="p-4">{t("admin.clientCompany.name")}</th>
            <th className="p-4">{t("admin.clientCompany.address")}</th>
            <th className="p-4">{t("admin.clientCompany.email")}</th>
            <th className="p-4">{t("admin.clientCompany.pec")}</th>
            <th className="p-4">{t("admin.clientCompany.landline")}</th>
            <th className="p-4">{t("admin.clientCompany.referentName")}</th>
            <th className="p-4">
              {t("admin.clientCompany.vehicleMobileNumber")}
            </th>
            <th className="p-4">{t("admin.clientCompany.referentEmail")}</th>
            <th className="p-4">{t("admin.vehicleVIN")}</th>
          </tr>
        </thead>
        <tbody>
          {clientData.map((client) => (
            <tr
              key={`${client.id}-${client.correspondingVehicleId}`}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2">
                <button
                  onClick={() => handleEditClick(client)}
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  title={t("admin.edit")}
                >
                  <Pencil size={16} />
                </button>
              </td>
              <td className="p-4">{client.vatNumber}</td>
              <td className="p-4">{client.name}</td>
              <td className="p-4">{client.address}</td>
              <td className="p-4">{client.email}</td>
              <td className="p-4">{client.pecAddress}</td>
              <td className="p-4">{client.landlineNumber}</td>
              <td className="p-4">{client.displayReferentName}</td>
              <td className="p-4">{client.displayVehicleMobileNumber}</td>
              <td className="p-4">{client.displayReferentEmail}</td>
              <td className="p-4">{client.correspondingVehicleVin}</td>
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
          onSearchTypeChange={setSearchType}
          vatLabel={t("admin.clientCompany.vatNumber")}
          companyLabel={t("admin.clientCompany.name")}
        />
      </div>

      {showEditModal && selectedClient && (
        <EditModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientCompany.editModal")}
        >
          <AdminClientCompanyEditForm
            client={selectedClient}
            onClose={() => setShowEditModal(false)}
            onSave={handleSave}
            refreshWorkflowData={async () =>
              await fetchClients(currentPage, query)
            }
            t={t}
          />
        </EditModal>
      )}
    </div>
  );
}
