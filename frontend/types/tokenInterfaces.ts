export interface ClientTeslaToken {
  id: number;
  teslaVehicleId: number;
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt?: string;
  createdAt: string;
  updatedAt: string;
}
