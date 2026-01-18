import { TFunction } from "i18next";
import TableOutagePeriods from "./tableOutagePeriods";

export default function TabOutages({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <TableOutagePeriods t={t} />
    </div>
  );
}
