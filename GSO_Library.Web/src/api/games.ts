import apiClient from './client';
import type { Game, PaginatedResult, PaginationParams } from '../types';

export const gamesApi = {
  list: (params?: PaginationParams) =>
    apiClient.get<PaginatedResult<Game>>('/games', { params }).then((r) => r.data),

  get: (id: number) =>
    apiClient.get<Game>(`/games/${id}`).then((r) => r.data),

  create: (data: Partial<Game>) =>
    apiClient.post<Game>('/games', data).then((r) => r.data),

  update: (id: number, data: Partial<Game>) =>
    apiClient.put<Game>(`/games/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/games/${id}`),
};
