export type FuelType = "Electric" | "Combustion";

export type VehicleOptionsType = {
  [brand: string]: {
    fuelType: FuelType;
    models: {
      [model: string]: {
        trims: string[];
        colors: string[];
      };
    };
  };
};

export const vehicleOptions: VehicleOptionsType = {
  Tesla: {
    fuelType: "Electric",
    models: {
      "Model 3": {
        trims: ["Long Range"],
        colors: ["Ultra Red"],
      },
    },
  },
  Polestar: {
    fuelType: "Electric",
    models: {
      "Polestar 4": {
        trims: ["Long range Single motor"],
        colors: ["Snow"],
      },
    },
  },
  Porsche: {
    fuelType: "Combustion",
    models: {
      "718 Cayman": {
        trims: ["GT4RS"],
        colors: ["Racing Yellow"],
      },
    },
  },
};
