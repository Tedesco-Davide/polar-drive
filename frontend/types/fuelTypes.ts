export enum FuelType {
  Electric = "Electric",
  Combustion = "Combustion",
}

export const fuelTypeOptions: { value: FuelType; label: string }[] = [
  { value: FuelType.Electric, label: "Electric" },
  { value: FuelType.Combustion, label: "Combustion" },
];
