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
    },
  },
  Polestar: {
    models: {
      "Polestar 4": {
        fuelType: FuelType.Electric,
        trims: ["Long range Single motor"],
        colors: ["Snow"],
      },
    },
  },
};
