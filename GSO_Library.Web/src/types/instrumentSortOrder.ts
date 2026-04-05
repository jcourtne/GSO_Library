import type { Instrument } from './instrument';

export interface InstrumentSortOrder {
  id: number;
  name: string;
  isDefault: boolean;
  instruments: Instrument[];
}
