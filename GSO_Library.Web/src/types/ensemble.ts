import type { Performance } from './performance';

export interface Ensemble {
  id: number;
  name: string;
  description?: string;
  website?: string;
  contactInfo?: string;
  performances?: Performance[];
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
}
