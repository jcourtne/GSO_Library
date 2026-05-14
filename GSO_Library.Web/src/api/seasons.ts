import apiClient from './client';
import type { Season, PaginatedResult, PaginationParams } from '../types';

export interface ShareConfig {
  includePdf: boolean;
  includeNotation: boolean;
  includePlayback: boolean;
  password?: string | null;
  clearPassword?: boolean;
}

export const seasonsApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Season>>('/seasons', { params, paramsSerializer: { indexes: null } }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Season>(`/seasons/${id}`).then((r) => r.data),

  create: (data: Partial<Season>) =>
    apiClient.post<Season>('/seasons', data).then((r) => r.data),

  update: (id: number, data: Partial<Season>) =>
    apiClient.put<Season>(`/seasons/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/seasons/${id}`),

  addArrangement: (seasonId: number, arrangementId: number) =>
    apiClient.post(`/seasons/${seasonId}/arrangements/${arrangementId}`),

  removeArrangement: (seasonId: number, arrangementId: number) =>
    apiClient.delete(`/seasons/${seasonId}/arrangements/${arrangementId}`),

  addPerformance: (seasonId: number, performanceId: number) =>
    apiClient.post(`/seasons/${seasonId}/performances/${performanceId}`),

  removePerformance: (seasonId: number, performanceId: number) =>
    apiClient.delete(`/seasons/${seasonId}/performances/${performanceId}`),

  configureShare: (id: number, config: ShareConfig) =>
    apiClient.post<{ token: string }>(`/seasons/${id}/share`, config).then((r) => r.data),

  revokeShare: (id: number) =>
    apiClient.delete(`/seasons/${id}/share`),
};
