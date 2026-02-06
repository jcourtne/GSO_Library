import apiClient from './client';
import type { Arrangement, ArrangementRequest, ArrangementFile, ArrangementFilterParams, PaginatedResult, PaginationParams } from '../types';

export const arrangementsApi = {
  list: (params?: PaginationParams & ArrangementFilterParams) =>
    apiClient.get<PaginatedResult<Arrangement>>('/arrangements', { params }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Arrangement>(`/arrangements/${id}`).then((r) => r.data),

  create: (data: ArrangementRequest) =>
    apiClient.post<Arrangement>('/arrangements', data).then((r) => r.data),

  update: (id: number, data: ArrangementRequest) =>
    apiClient.put<Arrangement>(`/arrangements/${id}/details`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/arrangements/${id}`),

  // Relationship endpoints
  addGame: (arrangementId: number, gameId: number) =>
    apiClient.post(`/arrangements/${arrangementId}/games/${gameId}`),

  removeGame: (arrangementId: number, gameId: number) =>
    apiClient.delete(`/arrangements/${arrangementId}/games/${gameId}`),

  addInstrument: (arrangementId: number, instrumentId: number) =>
    apiClient.post(`/arrangements/${arrangementId}/instruments/${instrumentId}`),

  removeInstrument: (arrangementId: number, instrumentId: number) =>
    apiClient.delete(`/arrangements/${arrangementId}/instruments/${instrumentId}`),

  addPerformance: (arrangementId: number, performanceId: number) =>
    apiClient.post(`/arrangements/${arrangementId}/performances/${performanceId}`),

  removePerformance: (arrangementId: number, performanceId: number) =>
    apiClient.delete(`/arrangements/${arrangementId}/performances/${performanceId}`),

  // File endpoints
  listFiles: (arrangementId: number) =>
    apiClient.get<ArrangementFile[]>(`/arrangements/${arrangementId}/files`).then((r) => r.data),

  uploadFile: (arrangementId: number, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return apiClient.post<ArrangementFile>(`/arrangements/${arrangementId}/files`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },

  downloadFile: (arrangementId: number, fileId: number) =>
    apiClient.get(`/arrangements/${arrangementId}/files/${fileId}`, {
      responseType: 'blob',
    }).then((r) => r),

  deleteFile: (arrangementId: number, fileId: number) =>
    apiClient.delete(`/arrangements/${arrangementId}/files/${fileId}`),
};
