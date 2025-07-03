export interface ApiErrorResponse {
  success: false;
  message: string;
  code?: string;
}

export interface ApiSuccessResponse {
  success: true;
  message: string;
  regenerationCount?: number;
}

export type ApiResponse = ApiErrorResponse | ApiSuccessResponse;
