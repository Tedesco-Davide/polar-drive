import { TFunction } from "i18next";
import DashboardVehicleWorkflow from "./dashboardVehicleWorkflow";
import TableVehicleWorkflow from "./tableVehicleWorkflow";
import TableClientVehicles from "./tableClientVehicles";

export default function TabVehicleWorkflow({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <DashboardVehicleWorkflow t={t} />
      <TableVehicleWorkflow t={t} />
      <TableClientVehicles t={t} />
    </div>
  );
}
