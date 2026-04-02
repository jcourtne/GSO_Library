import type { Game } from './game';
import type { Instrument } from './instrument';
import type { Performance } from './performance';

export interface ArrangementFile {
  id: number;
  fileName: string;
  storedFileName: string;
  contentType: string;
  fileSize: number;
  uploadedAt: string;
  createdBy?: string;
  arrangementId: number;
}

export interface Arrangement {
  id: number;
  name: string;
  description?: string;
  arrangers: string[];
  composers: string[];
  durationSeconds?: number;
  year?: number;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  files: ArrangementFile[];
  games: Game[];
  instruments: Instrument[];
  performances: Performance[];
}

export interface ArrangementRequest {
  name: string;
  description?: string;
  arrangers?: string[];
  composers?: string[];
  durationSeconds?: number;
  year?: number;
}

export interface ArrangementFilterParams {
  gameIds?: number[];
  seriesIds?: number[];
  instrumentIds?: number[];
  instrumentMatchAll?: boolean;
  performanceId?: number;
  composers?: string[];
  arrangers?: string[];
}

export interface FilterOption {
  id: number;
  name: string;
}

export interface ArrangementFilterOptions {
  composers: string[];
  arrangers: string[];
  games: FilterOption[];
  series: FilterOption[];
  instruments: FilterOption[];
}
