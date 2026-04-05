import type { Series } from './series';

export interface Game {
  id: number;
  name: string;
  description?: string;
  releaseYear?: number;
  seriesId: number;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  series?: Series;
}
