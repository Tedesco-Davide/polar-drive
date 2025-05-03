import { Pencil } from "lucide-react";
import { TFunction } from "i18next";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { usePagination } from "@/utils/usePagination";
import { useSearchFilter } from "@/utils/useSearchFilter";
import { useState } from "react";
import PaginationControls from "@/components/paginationControls";
import SearchBar from "@/components/searchBar";
import EditModal from "@/components/adminEditModal";
import AdminClientCompanyEditForm from "@/components/adminClientCompanyEditForm";

type Props = {
  clients: ClientCompany[];
  t: TFunction;
};

export default function AdminClientCompaniesTable({ clients, t }: Props) {
  const { query, setQuery, filteredData } = useSearchFilter<ClientCompany>(
    clients,
    [
      "companyVatNumber",
      "name",
      "address",
      "email",
      "pecAddress",
      "referentName",
      "referentEmail",
      "referentMobileNumber",
    ]
  );

  const {
    currentPage,
    totalPages,
    currentData: currentPageData,
    nextPage,
    prevPage,
    setCurrentPage,
  } = usePagination<ClientCompany>(filteredData, 5);

  const [selectedClient, setSelectedClient] = useState<ClientCompany | null>(
    null
  );
  const [showEditModal, setShowEditModal] = useState(false);

  const handleEditClick = (client: ClientCompany) => {
    setSelectedClient(client);
    setShowEditModal(true);
  };

  return (
    <div>
      <h1 className="text-2xl font-bold text-polarNight dark:text-softWhite mb-12">
        {t("admin.clientCompany.tableHeader")}
      </h1>

      <table className="w-full bg-softWhite dark:bg-polarNight text-sm rounded-lg overflow-hidden whitespace-nowrap">
        <thead className="bg-gray-200 dark:bg-gray-700 text-left border-b-2 border-polarNight dark:border-softWhite">
          <tr>
            <th className="p-4">{t("admin.actions")}</th>
            <th className="p-4">{t("admin.clientCompany.companyVatNumber")}</th>
            <th className="p-4">{t("admin.clientCompany.name")}</th>
            <th className="p-4">{t("admin.clientCompany.referentName")}</th>
            <th className="p-4">{t("admin.clientCompany.referentMobile")}</th>
            <th className="p-4">{t("admin.clientCompany.referentEmail")}</th>
            <th className="p-4">{t("admin.clientCompany.address")}</th>
            <th className="p-4">{t("admin.clientCompany.email")}</th>
            <th className="p-4">{t("admin.clientCompany.pec")}</th>
            <th className="p-4">{t("admin.clientCompany.referentPec")}</th>
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
              <td className="p-4">{client.companyVatNumber}</td>
              <td className="p-4">{client.name}</td>
              <td className="p-4">{client.referentName}</td>
              <td className="p-4">{client.referentMobileNumber}</td>
              <td className="p-4">{client.referentEmail}</td>
              <td className="p-4">{client.address}</td>
              <td className="p-4">{client.email}</td>
              <td className="p-4">{client.pecAddress}</td>
              <td className="p-4">{client.referentPecAddress}</td>
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
              console.log("Updated client:", updatedClient);
              setShowEditModal(false);
            }}
            t={t}
          />
        </EditModal>
      )}
    </div>
  );
}
