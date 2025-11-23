export interface UserModel {
  userId: number;
  fullName: string;
  email: string;
  orgId: number;
  username?: string;

  unitId: number | null;

  userParent: number;
}

export interface UserRelationModel {
  subordinates: UserModel[];
  peers: UserModel[];
}
export interface ChangePasswordRequest{
  oldPassword: string;
  newPassword: string;
}
export interface ChangePasswordResponse{
  
}