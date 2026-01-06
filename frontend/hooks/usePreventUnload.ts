import { useEffect } from 'react';

/**
 * Hook globale per prevenire la chiusura/refresh della pagina durante operazioni lunghe.
 * Usa questo hook in qualsiasi componente che esegue operazioni che non devono essere interrotte.
 *
 * @param isActive - true quando un'operazione è in corso
 * @example
 * usePreventUnload(isUploading || isGenerating);
 */
export function usePreventUnload(isActive: boolean) {
  useEffect(() => {
    if (!isActive) return;

    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
      return '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isActive]);
}
