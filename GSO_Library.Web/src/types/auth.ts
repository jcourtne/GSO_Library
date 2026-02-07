export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface UpdateCredentialsRequest {
  email?: string;
  currentPassword?: string;
  newPassword?: string;
}

export interface ResetPasswordRequest {
  newPassword: string;
}

export interface RoleManagementRequest {
  userId: string;
  role: string;
}

export interface AuthResponse {
  success: boolean;
  message: string;
  token?: string;
  refreshToken?: string;
  userId?: string;
  username?: string;
}

export interface RoleManagementResponse {
  success: boolean;
  message: string;
  userId?: string;
  username?: string;
  roles?: string[];
}

export interface UserResponse {
  id: string;
  userName?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
  isDisabled: boolean;
  roles: string[];
}
