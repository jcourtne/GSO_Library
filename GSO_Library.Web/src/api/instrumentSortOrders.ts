import apiClient from './client';
import type { Instrument, InstrumentSortOrder } from '../types';

export const instrumentSortOrdersApi = {
  list: () =>
    apiClient.get<InstrumentSortOrder[]>('/instrument-sort-orders').then((r) => r.data),

  getDefault: () =>
    apiClient.get<InstrumentSortOrder>('/instrument-sort-orders/default').then((r) => r.data),

  getInstruments: (id: number) =>
    apiClient.get<Instrument[]>(`/instrument-sort-orders/${id}/instruments`).then((r) => r.data),

  create: (data: { name: string; isDefault: boolean; instrumentIds: number[] }) =>
    apiClient.post<InstrumentSortOrder>('/instrument-sort-orders', data).then((r) => r.data),

  update: (id: number, data: { name: string; isDefault: boolean; instrumentIds: number[] }) =>
    apiClient.put<InstrumentSortOrder>(`/instrument-sort-orders/${id}`, data).then((r) => r.data),

  delete: (id: number) =>
    apiClient.delete(`/instrument-sort-orders/${id}`),
};
