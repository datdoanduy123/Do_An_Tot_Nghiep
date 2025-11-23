export interface AuthResponseModel {
  accessToken: string;
  refreshToken: string;
  userId: number;
  username: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  role: string;
}
