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

export interface PerformanceFile {
  id: number;
  performanceId: number;
  fileName: string;
  storedFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
  createdBy?: string;
}
