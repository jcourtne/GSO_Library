import apiClient from './client';
import type { InstrumentFamily } from '../types';

export const instrumentFamiliesApi = {
  getAll: () =>
    apiClient.get<InstrumentFamily[]>('/instrument-families').then((r) => r.data),
};
