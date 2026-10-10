export interface SeasonPublicData {
  name: string;
  ensembleName?: string;
  startDate?: string;
  endDate?: string;
  requiresPassword: boolean;
  arrangements: ArrangementSummary[];
  downloadSections: DownloadSection[];
}

export interface ArrangementSummary {
  id: number;
  name: string;
  composers: string[];
  arrangers: string[];
  games: string[];
}

export interface DownloadSection {
  label: string;
  scorePartType?: string | null;
  instrumentId?: number | null;
  familyId?: number | null;
  familyName?: string | null;
  lastUpdated?: string | null;
  fileCount: number;
  arrangementFileCounts?: Record<number, number>;
  arrangementLastUpdated?: Record<number, string>;
}
