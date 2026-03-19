import apiClient from './client';
import type { Performance, PerformanceFile, PaginatedResult, PaginationParams } from '../types';

export const performancesApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Performance>>('/performances', { params, paramsSerializer: { indexes: null } }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Performance>(`/performances/${id}`).then((r) => r.data),

  create: (data: Partial<Performance>) =>
    apiClient.post<Performance>('/performances', data).then((r) => r.data),

  update: (id: number, data: Partial<Performance>) =>
    apiClient.put<Performance>(`/performances/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/performances/${id}`),

  listFiles: (performanceId: number) =>
    apiClient.get<PerformanceFile[]>(`/performances/${performanceId}/files`).then((r) => r.data),

  uploadFile: (performanceId: number, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient.post<PerformanceFile>(
      `/performances/${performanceId}/files`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    ).then((r) => r.data);
  },

  downloadFile: (performanceId: number, fileId: number) =>
    apiClient.get(`/performances/${performanceId}/files/${fileId}`, {
      responseType: 'blob',
    }),

  deleteFile: (performanceId: number, fileId: number) =>
    apiClient.delete(`/performances/${performanceId}/files/${fileId}`),
};
