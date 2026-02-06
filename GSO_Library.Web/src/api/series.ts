import apiClient from './client';
import type { Series, PaginatedResult, PaginationParams } from '../types';

export const seriesApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Series>>('/series', { params }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Series>(`/series/${id}`).then((r) => r.data),

  create: (data: Partial<Series>) =>
    apiClient.post<Series>('/series', data).then((r) => r.data),

  update: (id: number, data: Partial<Series>) =>
    apiClient.put<Series>(`/series/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/series/${id}`),
};
