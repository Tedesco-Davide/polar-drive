import { useState, useEffect } from "react";
import { Trash2, PlusIcon } from "lucide-react";
import { TFunction } from "i18next";
import { logFrontendEvent } from "@/utils/logger";

type Props<T> = {
  entity: T;
  isOpen: boolean;
  title: string;
  notesField: keyof T;
  onSave: (updatedEntity: T) => void;
  onClose: () => void;
  t: TFunction;
};

export default function NotesModal<T>({
  entity,
  isOpen,
  title,
  notesField,
  onSave,
  onClose,
  t,
}: Props<T>) {
  const [notes, setNotes] = useState<string[]>(() => {
    const raw = entity[notesField] as unknown as string;

    // ✅ SOLUZIONE: Gestisci correttamente null, undefined e stringa vuota
    if (raw === null || raw === undefined || raw === "") {
      return [];
    }

    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [raw];
    } catch {
      // Se JSON.parse fallisce, tratta come stringa singola
      return typeof raw === "string" && raw.trim() !== "" ? [raw] : [];
    }
  });

  const [newNote, setNewNote] = useState<string>("");

  useEffect(() => {
    if (isOpen) {
      document.body.classList.add("overflow-hidden");
    } else {
      document.body.classList.remove("overflow-hidden");
    }
    return () => document.body.classList.remove("overflow-hidden");
  }, [isOpen]);

  if (!isOpen) return null;

  const handleAddNote = () => {
    if (!newNote.trim()) return;
    setNotes((prev) => [...prev, newNote.trim()]);
    logFrontendEvent("NotesModal", "INFO", "Note added", newNote);
    setNewNote("");
  };

  const handleEditNote = (index: number, value: string) => {
    const updated = [...notes];
    updated[index] = value;
    setNotes(updated);
    logFrontendEvent("NotesModal", "INFO", "Note edited", `Index: ${index}`);
  };

  const handleDeleteNote = (index: number) => {
    setNotes(notes.filter((_, i) => i !== index));
    logFrontendEvent("NotesModal", "INFO", "Note deleted", `Index: ${index}`);
  };

  // ✅ Modifica anche handleSave per gestire array vuoto
  const handleSave = () => {
    logFrontendEvent(
      "NotesModal",
      "INFO",
      "Notes saved",
      `Total notes: ${notes.length}`
    );

    // ✅ Se non ci sono note, salva null invece di array vuoto serializzato
    const notesToSave = notes.length > 0 ? JSON.stringify(notes) : null;

    onSave({
      ...entity,
      [notesField]: notesToSave,
    });
    onClose();
  };

  return (
    <div className="fixed top-[64px] md:top-[0px] inset-0 z-50 flex items-center justify-center bg-black/10 backdrop-blur-sm note-modal">
      <div className="w-full h-full p-6 relative overflow-y-auto bg-softWhite dark:bg-gray-800 border border-gray-300 dark:border-gray-600 shadow-none rounded-lg md:h-auto md:w-11/12">
        <h2 className="whitespace-normal text-xl font-semibold text-polarNight dark:text-softWhite mb-4">
          {title}
        </h2>

        {notes.map((note, index) => (
          <div key={index} className="mb-4 flex items-center gap-2">
            <button
              className="p-2 bg-dataRed text-white rounded hover:bg-red-600"
              onClick={() => handleDeleteNote(index)}
              title={t("admin.notes.deleteNote")}
            >
              <Trash2 size={16} />
            </button>
            <textarea
              className="w-full h-52 md:h-9 p-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-softWhite dark:bg-polarNight text-sm text-polarNight dark:text-softWhite"
              value={note}
              onChange={(e) => handleEditNote(index, e.target.value)}
            />
          </div>
        ))}

        <div className="mb-4 flex items-start gap-2">
          <button
            className="p-2 bg-blue-600 text-softWhite rounded hover:bg-blue-700"
            onClick={handleAddNote}
            title={t("admin.notes.addNote")}
          >
            <PlusIcon size={16} />
          </button>
          <textarea
            className="w-full h-40 md:h-20 p-4 border border-gray-600 rounded-lg text-sm bg-softWhite dark:bg-polarNight text-polarNight dark:text-softWhite placeholder-gray-400"
            value={newNote}
            onChange={(e) => setNewNote(e.target.value)}
            placeholder={
              t("admin.clientConsents.notes.modalPlaceholder") as string
            }
          />
        </div>

        <div className="mt-6 flex md:flex-row flex-col gap-4">
          <button
            className="bg-green-700 text-softWhite px-6 py-2 rounded hover:bg-green-600"
            onClick={handleSave}
          >
            {t("admin.confirmEditRow")}
          </button>
          <button
            className="bg-gray-400 text-white px-6 py-2 rounded hover:bg-gray-500"
            onClick={() => {
              logFrontendEvent(
                "NotesModal",
                "INFO",
                "Notes modal closed without saving"
              );
              onClose();
            }}
          >
            {t("admin.cancelEditRow")}
          </button>
        </div>
      </div>
    </div>
  );
}
