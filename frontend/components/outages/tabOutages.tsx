import { TFunction } from "i18next";
import DashboardOutagePeriods from "./tableOutagePeriodsDashboard";
import TableOutagePeriods from "./tableOutagePeriods";

export default function TabOutages({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <DashboardOutagePeriods t={t} />
      <TableOutagePeriods t={t} />
    </div>
  );
}
