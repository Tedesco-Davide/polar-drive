import { useState, useEffect } from "react";
import { motion } from "framer-motion";
import { Pencil, Building2 } from "lucide-react";
import { TFunction } from "i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/generic/paginationControls";
import SearchBar from "@/components/generic/searchBar";
import ModalEditTableRows from "@/components/generic/modalEditTableRows";
import Loader from "@/components/generic/loader";
import EditFormClientCompany from "./editFormClientCompany";

export default function TableClientCompanies({ t }: { t: TFunction }) {
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
        "TableClientCompanies",
        "INFO",
        "Clients loaded",
        `Page: ${data.page}, Total: ${data.totalCount}`
      );
    } catch (err) {
      logFrontendEvent(
        "TableClientCompanies",
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
      "TableClientCompanies",
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
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut" }}
      className="relative bg-white dark:bg-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
    >
      {(loading || isRefreshing) && <Loader local />}

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
            <div className="p-3 bg-gradient-to-br from-blue-400 to-indigo-500 rounded-xl shadow-md">
              <Building2 size={21} className="text-white" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-polarNight dark:text-softWhite">
                {t("admin.clientCompany.tableHeader")}
              </h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
                {totalCount} {t("admin.totals")}
              </p>
            </div>
          </div>
        </div>
      </div>

      <div className="p-6 overflow-x-auto">
        <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
          <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
            <tr>
              <th className="p-4 rounded-tl-lg">{t("admin.actions")}</th>
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
              <th className="p-4 rounded-tr-lg">{t("admin.vehicleVIN")}</th>
            </tr>
          </thead>
          <tbody>
            {clientData.map((client) => (
              <tr
                key={`${client.id}-${client.correspondingVehicleId}`}
                className="border-b border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
              >
                <td className="p-4">
                  <button
                    onClick={() => handleEditClick(client)}
                    className="p-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors shadow-sm hover:shadow-md"
                    title={t("admin.edit")}
                  >
                    <Pencil size={16} />
                  </button>
                </td>
                <td className="p-4 font-mono text-sm">{client.vatNumber}</td>
                <td className="p-4 font-medium">{client.name}</td>
                <td className="p-4 text-gray-600 dark:text-gray-400">{client.address}</td>
                <td className="p-4">{client.email}</td>
                <td className="p-4">{client.pecAddress}</td>
                <td className="p-4">{client.landlineNumber}</td>
                <td className="p-4">{client.displayReferentName}</td>
                <td className="p-4">{client.displayVehicleMobileNumber}</td>
                <td className="p-4">{client.displayReferentEmail}</td>
                <td className="p-4 font-mono text-sm">{client.correspondingVehicleVin}</td>
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
            searchMode="vin-or-company"
            externalSearchType={searchType}
            onSearchTypeChange={(type) => {
              if (type === "id" || type === "status") {
                setSearchType(type);
              }
            }}
            vatLabel={t("admin.clientCompany.vatNumber")}
            companyLabel={t("admin.clientCompany.name")}
            vinPlaceholder={t("admin.vehicles.searchVatPlaceholder")}
            companyPlaceholder={t("admin.vehicles.searchCompanyPlaceholder")}
          />
        </div>
      </div>

      {showEditModal && selectedClient && (
        <ModalEditTableRows
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientCompany.editModal")}
        >
          <EditFormClientCompany
            client={selectedClient}
            onClose={() => setShowEditModal(false)}
            onSave={handleSave}
            refreshWorkflowData={async () =>
              await fetchClients(currentPage, query)
            }
            t={t}
          />
        </ModalEditTableRows>
      )}
    </motion.div>
  );
}
