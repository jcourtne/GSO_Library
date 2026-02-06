import apiClient from './client';
import type { AuthResponse, LoginRequest, RegisterRequest, RefreshRequest, UpdateCredentialsRequest, RoleManagementRequest, RoleManagementResponse, UserResponse } from '../types';

export const authApi = {
  login: (data: LoginRequest) =>
    apiClient.post<AuthResponse>('/auth/login', data).then((r) => r.data),

  refresh: (data: RefreshRequest) =>
    apiClient.post<AuthResponse>('/auth/refresh', data).then((r) => r.data),

  revokeToken: (data: RefreshRequest) =>
    apiClient.post('/auth/revoke-token', data),

  updateCredentials: (data: UpdateCredentialsRequest) =>
    apiClient.put<AuthResponse>('/auth/update-credentials', data).then((r) => r.data),

  // Admin endpoints
  register: (data: RegisterRequest) =>
    apiClient.post<AuthResponse>('/auth/register', data).then((r) => r.data),

  getUsers: () =>
    apiClient.get<UserResponse[]>('/auth/users').then((r) => r.data),

  getUser: (id: string) =>
    apiClient.get<UserResponse>(`/auth/users/${id}`).then((r) => r.data),

  disableUser: (userId: string) =>
    apiClient.post<AuthResponse>(`/auth/disable/${userId}`).then((r) => r.data),

  enableUser: (userId: string) =>
    apiClient.post<AuthResponse>(`/auth/enable/${userId}`).then((r) => r.data),

  grantRole: (data: RoleManagementRequest) =>
    apiClient.post<RoleManagementResponse>('/auth/grant-role', data).then((r) => r.data),

  removeRole: (data: RoleManagementRequest) =>
    apiClient.post<RoleManagementResponse>('/auth/remove-role', data).then((r) => r.data),
};
