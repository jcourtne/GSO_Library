import type { Ensemble } from './ensemble';
import type { Arrangement } from './arrangement';
import type { Performance } from './performance';

export interface Season {
  id: number;
  name: string;
  ensembleId: number;
  ensemble?: Ensemble;
  startDate?: string;
  endDate?: string;
  notes?: string;
  arrangements?: Arrangement[];
  performances?: Performance[];
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  shareToken?: string | null;
  shareIncludePdf?: boolean;
  shareIncludeNotation?: boolean;
  shareIncludePlayback?: boolean;
  hasSharePassword?: boolean;
}
