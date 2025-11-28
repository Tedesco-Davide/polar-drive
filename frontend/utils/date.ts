import { format, parseISO, isValid } from "date-fns";

export const formatDateToDisplay = (isoDate: string | null | undefined) => {
  if (!isoDate) return "";
  const parsedDate = parseISO(isoDate);
  return isValid(parsedDate) ? format(parsedDate, "dd/MM/yyyy") : "";
};

export const formatDateToSave = (date: Date) => format(date, "yyyy-MM-dd");

export const formatOutageDateTimeToSave = (date: Date): string =>
  format(date, "yyyy-MM-dd'T'HH:mm");