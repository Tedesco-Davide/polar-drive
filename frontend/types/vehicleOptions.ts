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

export const vehicleOptions: VehicleOptionsType = {
  Tesla: {
    models: {
      "Model 3": {
        fuelType: FuelType.Electric,
        trims: ["Long Range"],
        colors: ["Ultra Red"],
      },
      "Model Y": {
        fuelType: FuelType.Electric,
        trims: ["Performance"],
        colors: ["Pearl White"],
      },
    },
  },
};
