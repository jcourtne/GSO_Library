import apiClient from './client';
import type { Performance, PaginatedResult, PaginationParams } from '../types';

export const performancesApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Performance>>('/performances', { params }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Performance>(`/performances/${id}`).then((r) => r.data),

  create: (data: Partial<Performance>) =>
    apiClient.post<Performance>('/performances', data).then((r) => r.data),

  update: (id: number, data: Partial<Performance>) =>
    apiClient.put<Performance>(`/performances/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/performances/${id}`),
};
