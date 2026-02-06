import apiClient from './client';
import type { Instrument, PaginatedResult, PaginationParams } from '../types';

export const instrumentsApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Instrument>>('/instruments', { params }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Instrument>(`/instruments/${id}`).then((r) => r.data),

  create: (data: Partial<Instrument>) =>
    apiClient.post<Instrument>('/instruments', data).then((r) => r.data),

  update: (id: number, data: Partial<Instrument>) =>
    apiClient.put<Instrument>(`/instruments/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/instruments/${id}`),
};
