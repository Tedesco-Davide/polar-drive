import { useState, useEffect } from 'react';
import { VehicleOptionsType, loadVehicleOptions } from '@/types/vehicleOptions';

/**
 * Hook React per caricare le opzioni veicoli dall'API
 * Gestisce loading state e fallback automatico
 */
export function useVehicleOptions() {
  const [options, setOptions] = useState<VehicleOptionsType>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let mounted = true;

    const fetchOptions = async () => {
      try {
        const data = await loadVehicleOptions();
        if (mounted) {
          setOptions(data);
          setError(null);
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err : new Error('Unknown error'));
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };

    fetchOptions();

    return () => {
      mounted = false;
    };
  }, []);

  return { options, loading, error };
}
