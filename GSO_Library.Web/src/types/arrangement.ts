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
  arranger?: string;
  composer?: string;
  key?: string;
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
  arranger?: string;
  composer?: string;
  key?: string;
  durationSeconds?: number;
  year?: number;
}

export interface ArrangementFilterParams {
  gameId?: number;
  seriesId?: number;
  instrumentId?: number;
  performanceId?: number;
}
