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
        trims: [
          "Trazione posteriore",
          "Long Range a trazione posteriore",
          "Long Range a trazione integrale",
          "Performance a trazione integrale"
        ],
        colors: [
          "Bianco Perla",
          "Blu Oceano",
          "Nero Diamante",
          "Grigio Stealth",
          "Ultra Rosso",
          "Argento Mercurio"
        ],
      },
      "Model Y": {
        fuelType: FuelType.Electric,
        trims: [
          "Trazione posteriore",
          "Long Range a trazione posteriore",
          "Long Range a trazione integrale",
          "Performance a trazione integrale"
        ],
        colors: [
          "Bianco Perla",
          "Blu Oceano",
          "Nero Diamante",
          "Grigio Stealth",
          "Ultra Rosso",
          "Argento Mercurio"
        ],
      },
      "Model S": {
        fuelType: FuelType.Electric,
        trims: [
          "Trazione integrale",
          "Plaid"
        ],
        colors: [
          "Bianco Perla",
          "Blu Frost",
          "Nero Diamante",
          "Grigio Stealth",
          "Ultra Rosso",
          "Argento Lunare"
        ],
      },
      "Model X": {
        fuelType: FuelType.Electric,
        trims: [
          "Trazione integrale",
          "Plaid"
        ],
        colors: [
          "Bianco Perla",
          "Blu Frost",
          "Nero Diamante",
          "Grigio Stealth",
          "Ultra Rosso",
          "Argento Lunare"
        ],
      },
    },
  },
};
