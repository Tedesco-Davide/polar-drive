import { FuelType } from "./fuelTypes";

export type VehicleOptionsType = {
  [brand: string]: {
    models: {
      [model: string]: {
        fuelType: FuelType;
        trims: string[];
        colors: string[];
      };
    };
  };
};

// Fallback statico in caso di errore nel caricamento da API
const fallbackVehicleOptions: VehicleOptionsType = {
  Tesla: {
    models: {
      "Model 3": {
        fuelType: FuelType.Electric,
        trims: ["Trazione posteriore"],
        colors: ["Bianco Perla"],
      },
    },
  },
};

let cachedVehicleOptions: VehicleOptionsType | null = null;

// Carica le opzioni veicoli dall'API backend

export async function loadVehicleOptions(): Promise<VehicleOptionsType> {
  if (cachedVehicleOptions) {
    return cachedVehicleOptions;
  }

  try {
    // Usa URL relativo per passare attraverso il proxy di Next.js
    const response = await fetch('/api/VehicleOptions', {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
      },
      cache: 'no-store', // Sempre fresh data
    });

    if (!response.ok) {
      throw new Error(`Failed to load vehicle options: ${response.status}`);
    }

    const data = await response.json();
    
    // Trasforma la risposta API nel formato atteso dal frontend
    const options: VehicleOptionsType = {};
    
    if (data.options) {
      for (const [brandName, brandData] of Object.entries(data.options as Record<string, { models?: Record<string, { fuelType?: string; trims?: string[]; colors?: string[] }> }>)) {
        options[brandName] = {
          models: {}
        };

        if (brandData.models) {
          for (const [modelName, modelData] of Object.entries(brandData.models)) {
            options[brandName].models[modelName] = {
              fuelType: (modelData.fuelType as FuelType) || FuelType.Electric,
              trims: modelData.trims || [],
              colors: modelData.colors || [],
            };
          }
        }
      }
    }

    cachedVehicleOptions = options;
    return options;
  } catch (error) {
    cachedVehicleOptions = fallbackVehicleOptions;
    return fallbackVehicleOptions;
  }
}

// Reset della cache (utile per forzare il reload)

export function resetVehicleOptionsCache(): void {
  cachedVehicleOptions = null;
}

