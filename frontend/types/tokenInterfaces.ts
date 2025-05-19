export interface ClientVehicleToken {
  id: number;
  vehicleId: number;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt?: string;
  createdAt: string;
  updatedAt: string;
}
