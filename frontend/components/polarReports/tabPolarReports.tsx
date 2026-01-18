import { TFunction } from "i18next";
import DashboardGapValidation from "./tablePdfReportsDashboardGapValidation";
import TablePdfReports from "./tablePdfReports";

export default function TabPolarReports({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      {/* Card 1: Alert Gap Dashboard */}
      <DashboardGapValidation t={t} />

      {/* Card 2: PDF Reports Table */}
      <TablePdfReports t={t} />
    </div>
  );
}
