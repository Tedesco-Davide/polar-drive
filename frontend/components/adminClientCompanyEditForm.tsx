import { useState } from "react";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { TFunction } from "i18next";
import { API_BASE_URL } from "@/utils/api";

type Props = {
  client: ClientCompany;
  onClose: () => void;
  onSave: (updatedClient: ClientCompany) => void;
  refreshWorkflowData: () => Promise<void>;
  t: TFunction;
};

export default function AdminClientCompanyEditForm({
  client,
  onClose,
  onSave,
  refreshWorkflowData,
  t,
}: Props) {
  const [formData, setFormData] = useState<ClientCompany>({
    ...client,
    address: client.address ?? "",
    email: client.email ?? "",
    pecAddress: client.pecAddress ?? "",
    landlineNumber: client.landlineNumber ?? "",
    referentPecAddress: client.referentPecAddress ?? "",
  });

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async () => {
    const isEmailValid = (email: string) =>
      /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

    // ✅ Partita IVA
    if (!/^[0-9]{11}$/.test(formData.vatNumber.trim())) {
      alert(t("admin.validation.invalidVat"));
      return;
    }

    // ✅ Nome azienda obbligatorio
    if (!formData.name.trim()) {
      alert(t("admin.validation.companyName"));
      return;
    }

    // ✅ Nome azienda obbligatorio
    if (!formData.referentName.trim()) {
      alert(t("admin.validation.referentName"));
      return;
    }

    // ✅ Cellulare referente obbligatorio + esattamente 10 cifre
    if (
      !formData.referentMobileNumber.trim() ||
      !/^[0-9]{10}$/.test(formData.referentMobileNumber.trim())
    ) {
      alert(t("admin.validation.invalidMobile"));
      return;
    }

    // ✅ Email referente obbligatoria + valida
    if (
      !formData.referentEmail.trim() ||
      !isEmailValid(formData.referentEmail.trim())
    ) {
      alert(t("admin.validation.invalidReferentEmail"));
      return;
    }

    // ✅ Email aziendale: se presente, deve essere valida
    if (formData.email.trim() && !isEmailValid(formData.email.trim())) {
      alert(t("admin.validation.invalidEmail"));
      return;
    }

    // ✅ PEC aziendale: se presente, deve essere valida
    if (
      (formData.pecAddress ?? "").trim() &&
      !isEmailValid((formData.pecAddress ?? "").trim())
    ) {
      alert(t("admin.validation.invalidCompanyPec"));
      return;
    }

    // ✅ Telefono fisso: se presente, max 11 numeri
    if (
      formData.landlineNumber.trim() &&
      !/^[0-9]{1,11}$/.test(formData.landlineNumber.trim())
    ) {
      alert(t("admin.validation.invalidLandline"));
      return;
    }

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/ClientCompanies/${formData.id}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(formData),
        }
      );

      if (!response.ok) {
        throw new Error("Errore nel salvataggio");
      }

      // ✅ Successo
      alert(t("admin.successEditRow"));
      onSave(formData);
      await refreshWorkflowData();
    } catch (err) {
      console.error(t("admin.genericApiError"), err);
      alert(err instanceof Error ? err.message : t("admin.genericApiError"));
    }

    onSave(formData);
  };

  return (
    <div className="bg-softWhite dark:bg-gray-800 p-6 rounded-lg shadow-lg border border-gray-300 dark:border-gray-600">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.vatNumber")}
          </span>
          <input
            maxLength={11}
            pattern="[0-9]*"
            inputMode="numeric"
            name="vatNumber"
            value={formData.vatNumber}
            onChange={handleChange}
            className="input bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400 cursor-not-allowed"
            disabled
            readOnly
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.name")}
          </span>
          <input
            name="name"
            value={formData.name}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.address")}
          </span>
          <input
            name="address"
            value={formData.address}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.email")}
          </span>
          <input
            type="email"
            name="email"
            value={formData.email}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.pec")}
          </span>
          <input
            type="email"
            name="pecAddress"
            value={formData.pecAddress}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentPec")}
          </span>
          <input
            name="referentPecAddress"
            value={formData.referentPecAddress}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentName")}
          </span>
          <input
            name="referentName"
            value={formData.referentName}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentMobile")}
          </span>
          <input
            maxLength={10}
            pattern="[0-9]*"
            inputMode="numeric"
            name="referentMobileNumber"
            value={formData.referentMobileNumber}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.referentEmail")}
          </span>
          <input
            type="email"
            name="referentEmail"
            value={formData.referentEmail}
            onChange={handleChange}
            className="input"
          />
        </label>
        <label className="flex flex-col">
          <span className="text-sm text-gray-600 dark:text-gray-300 mb-1">
            {t("admin.clientCompany.landline")}
          </span>
          <input
            maxLength={11}
            pattern="[0-9]*"
            inputMode="numeric"
            name="landlineNumber"
            value={formData.landlineNumber}
            onChange={handleChange}
            className="input"
          />
        </label>
      </div>
      <div className="mt-6 flex md:flex-row flex-col gap-4">
        <button
          className="bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
          onClick={handleSubmit}
        >
          {t("admin.confirmEditRow")}
        </button>
        <button
          className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
          onClick={onClose}
        >
          {t("admin.cancelEditRow")}
        </button>
      </div>
    </div>
  );
}
