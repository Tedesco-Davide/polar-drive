import { useState, useEffect } from "react";
import { Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { logFrontendEvent } from "@/utils/logger";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import EditModal from "@/components/adminEditModal";
import AdminClientCompanyEditForm from "@/components/adminClientCompanyEditForm";
import AdminLoader from "@/components/adminLoader";

type Props = {
  t: TFunction;
  clients: ClientCompany[];
  refreshWorkflowData: () => Promise<void>;
};

export default function AdminClientCompaniesTable({
  t,
  clients,
  refreshWorkflowData,
}: Props) {
  const [loading, setLoading] = useState(true);
  const [clientData, setClientData] = useState<ClientCompany[]>([]);

  useEffect(() => {
    setClientData(clients);
    setLoading(false);
    logFrontendEvent(
      "AdminClientCompaniesTable",
      "INFO",
      "Component mounted and client data loaded",
      `Loaded ${clients.length} clients`
    );
  }, [clients]);

  const { query, setQuery, filteredData } = useSearchFilter<ClientCompany>(
    clientData,
    [
      "vatNumber",
      "name",
      "address",
      "email",
      "pecAddress",
      "displayReferentName",
      "displayReferentEmail",
      "displayReferentMobile",
      "displayReferentPec",
    ]
  );

  useEffect(() => {
    logFrontendEvent(
      "AdminClientCompaniesTable",
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
  } = usePagination<ClientCompany>(filteredData, 5);

  useEffect(() => {
    logFrontendEvent(
      "AdminClientCompaniesTable",
      "DEBUG",
      "Pagination changed",
      `Current page: ${currentPage}`
    );
  }, [currentPage]);

  const [selectedClient, setSelectedClient] = useState<ClientCompany | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

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

  return (
    <div>
      {loading && <AdminLoader />}

      <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite mb-12">
        {t("admin.clientCompany.tableHeader")}
      </h1>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
            <th className="p-4">{t("admin.clientCompany.vatNumber")}</th>
            <th className="p-4">{t("admin.clientCompany.name")}</th>
            <th className="p-4">
              {t("admin.clientCompany.referentName")}
              <small className="block text-xs opacity-70">(dal veicolo)</small>
            </th>
            <th className="p-4">
              {t("admin.clientCompany.referentMobile")}
              <small className="block text-xs opacity-70">(dal veicolo)</small>
            </th>
            <th className="p-4">
              {t("admin.clientCompany.referentEmail")}
              <small className="block text-xs opacity-70">(dal veicolo)</small>
            </th>
            <th className="p-4">{t("admin.clientCompany.address")}</th>
            <th className="p-4">{t("admin.clientCompany.email")}</th>
            <th className="p-4">{t("admin.clientCompany.pec")}</th>
            <th className="p-4">
              {t("admin.clientCompany.referentPec")}
              <small className="block text-xs opacity-70">(dal veicolo)</small>
            </th>
            <th className="p-4">{t("admin.clientCompany.landline")}</th>
          </tr>
        </thead>
        <tbody>
          {currentPageData.map((client) => (
            <tr
              key={client.id}
              className="border-b border-gray-300 dark:border-gray-600"
            >
              <td className="p-4 space-x-2">
                <button
                  className="p-2 bg-blue-500 text-softWhite rounded hover:bg-blue-600"
                  onClick={() => handleEditClick(client)}
                  title={t("admin.edit")}
                >
                  <Pencil size={16} />
                </button>
              </td>
              <td className="p-4">{client.vatNumber}</td>
              <td className="p-4">{client.name}</td>
              <td className="p-4">{client.displayReferentName}</td>
              <td className="p-4">{client.displayReferentMobile}</td>
              <td className="p-4">{client.displayReferentEmail}</td>
              <td className="p-4">{client.address}</td>
              <td className="p-4">{client.email}</td>
              <td className="p-4">{client.pecAddress}</td>
              <td className="p-4">{client.displayReferentPec}</td>
              <td className="p-4">{client.landlineNumber}</td>
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

      {/* 🔽 MODALE DI EDIT */}
      {showEditModal && selectedClient && (
        <EditModal
          isOpen={showEditModal}
          onClose={() => setShowEditModal(false)}
          title={t("admin.clientCompany.editModal")}
        >
          <AdminClientCompanyEditForm
            client={selectedClient}
            onClose={() => setShowEditModal(false)}
            onSave={(updatedClient: ClientCompany) => {
              setClientData((prev) =>
                prev.map((c) => (c.id === updatedClient.id ? updatedClient : c))
              );
              setShowEditModal(false);
              logFrontendEvent(
                "AdminClientCompaniesTable",
                "INFO",
                "Client update saved successfully",
                `ClientId: ${updatedClient.id}, VAT: ${updatedClient.vatNumber}`
              );
            }}
            refreshWorkflowData={refreshWorkflowData}
            t={t}
          />
        </EditModal>
      )}
    </div>
  );
}
