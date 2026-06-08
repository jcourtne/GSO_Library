import type { Performance } from './performance';
import type { Season } from './season';

export interface EnsembleMember {
  id: string;
  userName?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
}

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
