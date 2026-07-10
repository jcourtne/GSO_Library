export interface Instrument {
  id: number;
  name: string;
  familyId?: number | null;
  familyName?: string | null;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
}
