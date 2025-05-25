export type VehicleOptionsType = {
  [brand: string]: {
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
    models: {
      "Model 3": {
        trims: ["Long Range"],
        colors: ["Ultra Red"],
      },
    },
  },
  Polestar: {
    models: {
      "Polestar 4": {
        trims: ["Long range Single motor"],
        colors: ["Snow"],
      },
    },
  },
  Porsche: {
    models: {
      "718 Cayman": {
        trims: ["GT4RS"],
        colors: ["Racing Yellow"],
      },
    },
  },
};
