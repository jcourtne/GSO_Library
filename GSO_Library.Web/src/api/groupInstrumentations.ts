import apiClient from './client';
import type { GroupInstrumentation, PaginatedResult, PaginationParams } from '../types';

export const groupInstrumentationsApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<GroupInstrumentation>>('/group-instrumentations', { params, paramsSerializer: { indexes: null } }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<GroupInstrumentation>(`/group-instrumentations/${id}`).then((r) => r.data),

  create: (data: Partial<GroupInstrumentation>) =>
    apiClient.post<GroupInstrumentation>('/group-instrumentations', data).then((r) => r.data),

  update: (id: number, data: Partial<GroupInstrumentation>) =>
    apiClient.put<GroupInstrumentation>(`/group-instrumentations/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/group-instrumentations/${id}`),
};
