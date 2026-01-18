import { TFunction } from "i18next";
import DashboardClientCompanies from "./tableClientCompaniesDashboard";
import TableClientCompanies from "./tableClientCompanies";
import TableClientConsents from "./tableClientConsents";

export default function TabClientCompanies({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <DashboardClientCompanies t={t} />
      <TableClientCompanies t={t} />
      <TableClientConsents t={t} />
    </div>
  );
}
