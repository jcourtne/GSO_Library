import type { Ensemble } from './ensemble';

export interface Performance {
  id: number;
  name: string;
  link: string;
  performanceDate?: string;
  notes?: string;
  ensembleId?: number;
  ensemble?: Ensemble;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
}
