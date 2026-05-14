import type { Performance } from './performance';
import type { Season } from './season';

export interface Ensemble {
  id: number;
  name: string;
  description?: string;
  website?: string;
  contactInfo?: string;
  performances?: Performance[];
  seasons?: Season[];
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
}
