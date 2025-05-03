import { useState, useMemo } from "react";

export function useSearchFilter<T>(data: T[], keysToSearch: (keyof T)[]) {
  const [query, setQuery] = useState("");

  const filteredData = useMemo(() => {
    if (!query.trim()) return data;

    const lowerQuery = query.toLowerCase();

    return data.filter((item) =>
      keysToSearch.some((key) => {
        const value = item[key];
        if (value === null || value === undefined) return false;

        return value.toString().toLowerCase().includes(lowerQuery);
      })
    );
  }, [query, data, keysToSearch]);

  return {
    query,
    setQuery,
    filteredData,
  };
}
