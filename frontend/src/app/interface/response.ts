export interface ResponseApi<T = any> {
  success: boolean;
  data: T;
  error?: string;
  message: string;
}
export interface changePasswordResponse{
  success: boolean;
  data: null;
  error?:string;
  message: string;
}