import { TFunction } from "i18next";
import TableFileManager from "./tableFileManager";

export default function TabFileManager({ t }: { t: TFunction }) {
  return (
    <div className="space-y-6">
      <TableFileManager t={t} />
    </div>
  );
}
