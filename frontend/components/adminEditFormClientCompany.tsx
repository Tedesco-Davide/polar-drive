import { useState } from "react";
import { ClientCompany } from "@/types/clientCompanyInterfaces";
import { TFunction } from "i18next";
import { logFrontendEvent } from "@/utils/logger";

type AdminEditFormClientCompanyProps = {
  client: ClientCompany;
  onClose: () => void;
  onSave: (updatedClient: ClientCompany) => void;
  refreshWorkflowData: () => Promise<void>;
  t: TFunction;
};

export default function AdminEditFormClientCompany({
  client,
  onClose,
  onSave,
  refreshWorkflowData,
  t,
}: AdminEditFormClientCompanyProps) {
  const [formData, setFormData] = useState<ClientCompany>({
    ...client,
    address: client.address ?? "",
    email: client.email ?? "",
    pecAddress: client.pecAddress ?? "",
    landlineNumber: client.landlineNumber ?? "",
    // MAPPA i display fields ai campi che il form usa
    referentName: client.displayReferentName ?? "",
    vehicleMobileNumber: client.displayVehicleMobileNumber ?? "",
    referentEmail: client.displayReferentEmail ?? "",
    // AGGIUNGI anche il correspondingVehicleId
    correspondingVehicleId: client.correspondingVehicleId,
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

    // ✅ CORRETTO - valida referent*
    if (!formData.referentName?.trim()) {
      alert(t("admin.validation.referentName"));
      return;
    }

    // ✅ Cellulare referente obbligatorio + esattamente 10 cifre
    if (
      !formData.vehicleMobileNumber?.trim() ||
      !/^(\+39)?[0-9]{10}$/.test(formData.vehicleMobileNumber.trim())
    ) {
      alert(t("admin.validation.invalidMobile"));
      return;
    }

    // ✅ Email referente obbligatoria + valida
    if (
      !formData.referentEmail?.trim() ||
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
      await logFrontendEvent(
        "AdminEditFormClientCompany",
        "INFO",
        "Attempting to update client company and referent data",
        JSON.stringify(formData)
      );

      // 1. SALVA I DATI AZIENDA (senza referenti)
      const companyData = {
        id: formData.id,
        vatNumber: formData.vatNumber,
        name: formData.name,
        address: formData.address,
        email: formData.email,
        pecAddress: formData.pecAddress,
        landlineNumber: formData.landlineNumber,
      };

      const companyResponse = await fetch(
        `/api/ClientCompanies/${formData.id}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(companyData),
        }
      );

      if (!companyResponse.ok) {
        throw new Error(
          "Failed to update company. Status: " + companyResponse.status
        );
      }

      // AGGIORNA IL VEICOLO SPECIFICO
      if (formData.correspondingVehicleId) {
        // Recupera il veicolo corrente
        const vehicleResponse = await fetch(
          `/api/ClientVehicles/${formData.correspondingVehicleId}`
        );

        if (!vehicleResponse.ok) {
          throw new Error(
            "Failed to fetch vehicle. Status: " + vehicleResponse.status
          );
        }

        const currentVehicle = await vehicleResponse.json();

        // Crea il DTO completo con TUTTI i campi richiesti
        const updatedVehicleData = {
          id: currentVehicle.id,
          clientCompanyId: currentVehicle.clientCompany.id,
          vin: currentVehicle.vin,
          fuelType: currentVehicle.fuelType,
          brand: currentVehicle.brand,
          model: currentVehicle.model,
          trim: currentVehicle.trim || "",
          color: currentVehicle.color || "",
          isActive: currentVehicle.isActive,
          isFetching: currentVehicle.isFetching,
          firstActivationAt: currentVehicle.firstActivationAt,
          lastDeactivationAt: currentVehicle.lastDeactivationAt,
          lastFetchingDataAt: currentVehicle.lastFetchingDataAt,
          // Solo questi campi vengono aggiornati
          referentName: formData.referentName,
          vehicleMobileNumber: formData.vehicleMobileNumber,
          referentEmail: formData.referentEmail,
        };

        const updateResponse = await fetch(
          `/api/ClientVehicles/${formData.correspondingVehicleId}`,
          {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(updatedVehicleData),
          }
        );

        if (!updateResponse.ok) {
          const errorText = await updateResponse.text();
          try {
            const json = JSON.parse(errorText);
            if (json.errorCode === "MOBILE_NUMBER_ALREADY_USED_BY_ANOTHER_COMPANY") {
              alert(t("admin.mobileNumberAlreadyUsedByAnotherCompany"));
              return;
            }
          } catch {
            // Not JSON, continue with generic error
          }
          throw new Error(
            "Failed to update vehicle. Status: " + updateResponse.status
          );
        }
      }

      alert(t("admin.successEditRow"));
      const updatedClient: ClientCompany = {
        ...client, // Mantieni i campi originali
        // Aggiorna i dati azienda con quelli del form
        name: formData.name,
        address: formData.address,
        email: formData.email,
        pecAddress: formData.pecAddress,
        landlineNumber: formData.landlineNumber,
        // Aggiorna i dati referente con quelli del form
        displayReferentName: formData.referentName,
        displayVehicleMobileNumber: formData.vehicleMobileNumber,
        displayReferentEmail: formData.referentEmail,
        // Mantieni gli identificatori
        correspondingVehicleId: formData.correspondingVehicleId,
        correspondingVehicleVin: client.correspondingVehicleVin,
      };

      onSave(updatedClient);
      await refreshWorkflowData();
    } catch (err) {
      const errorDetails = err instanceof Error ? err.message : String(err);

      await logFrontendEvent(
        "AdminEditFormClientCompany",
        "ERROR",
        "Exception thrown during client update",
        errorDetails
      );

      console.error(t("admin.genericApiError"), err);
      alert(t("admin.genericApiError"));
    }
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
            {t("admin.clientCompany.vehicleMobileNumber")}
          </span>
          <input
            maxLength={10}
            pattern="[0-9]*"
            inputMode="numeric"
            name="vehicleMobileNumber"
            value={formData.vehicleMobileNumber}
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
