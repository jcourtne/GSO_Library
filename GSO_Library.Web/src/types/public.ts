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
  name: string;
  composers: string[];
  arrangers: string[];
}

export interface DownloadSection {
  label: string;
  scorePartType?: string | null;
  instrumentId?: number | null;
  lastUpdated?: string | null;
  fileCount: number;
}
