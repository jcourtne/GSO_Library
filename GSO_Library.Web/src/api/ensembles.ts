import apiClient from './client';
import type { Ensemble, PaginatedResult, PaginationParams } from '../types';

export const ensemblesApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Ensemble>>('/ensembles', { params }).then((r) => r.data),

  getAll: () =>
    apiClient.get<Ensemble[]>('/ensembles', { params: { page: 1, pageSize: 100 } })
      .then((r) => (r.data as unknown as PaginatedResult<Ensemble>).items),

  get: (id: number) =>
    apiClient.get<Ensemble>(`/ensembles/${id}`).then((r) => r.data),

  create: (data: Partial<Ensemble>) =>
    apiClient.post<Ensemble>('/ensembles', data).then((r) => r.data),

  update: (id: number, data: Partial<Ensemble>) =>
    apiClient.put<Ensemble>(`/ensembles/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/ensembles/${id}`),
};
