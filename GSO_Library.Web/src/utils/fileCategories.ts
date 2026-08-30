import type { ArrangementFile } from '../types';
import { SCORE_PART_TYPES } from './scorePartTypes';

const NOTATION_EXTENSIONS = ['.xml', '.mxl', '.mscz', '.dorico', '.sib'];
const RENDERED_SCORE_EXTENSIONS = ['.pdf'];
const PLAYBACK_EXTENSIONS = ['.mid', '.midi', '.mp3', '.wav', '.flac', '.ogg'];

export interface CategorizedFiles {
  notationFiles: ArrangementFile[];
  renderedScoreFiles: ArrangementFile[];
  playbackFiles: ArrangementFile[];
}

function getExtension(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot >= 0 ? fileName.slice(dot).toLowerCase() : '';
}

export function categorizeFiles(files: ArrangementFile[]): CategorizedFiles {
  const notationFiles: ArrangementFile[] = [];
  const renderedScoreFiles: ArrangementFile[] = [];
  const playbackFiles: ArrangementFile[] = [];

  for (const file of files) {
    const ext = getExtension(file.fileName);
    if (NOTATION_EXTENSIONS.includes(ext)) {
      notationFiles.push(file);
    } else if (RENDERED_SCORE_EXTENSIONS.includes(ext)) {
      renderedScoreFiles.push(file);
    } else if (PLAYBACK_EXTENSIONS.includes(ext)) {
      playbackFiles.push(file);
    } else {
      // Uncategorized files go into notation files as a fallback
      notationFiles.push(file);
    }
  }

  return { notationFiles, renderedScoreFiles, playbackFiles };
}

export interface GroupedScoreFiles {
  conductorScore: ArrangementFile[];
  byInstrument: Map<number, ArrangementFile[]>;
  percussion: ArrangementFile[];
  voice: ArrangementFile[];
  unlisted: ArrangementFile[];
}

export function groupScoreFiles(
  renderedFiles: ArrangementFile[],
  arrangementInstrumentIds: Set<number>,
): GroupedScoreFiles {
  const result: GroupedScoreFiles = {
    conductorScore: [],
    byInstrument: new Map(),
    percussion: [],
    voice: [],
    unlisted: [],
  };
  for (const f of renderedFiles) {
    let categorized = false;

    if (f.scorePartType === SCORE_PART_TYPES.CONDUCTOR_SCORE) {
      result.conductorScore.push(f);
      categorized = true;
    }
    if (f.scorePartType === SCORE_PART_TYPES.PERCUSSION_PART) {
      result.percussion.push(f);
      categorized = true;
    }
    if (f.scorePartType === SCORE_PART_TYPES.VOICE_PART) {
      result.voice.push(f);
      categorized = true;
    }

    for (const instId of f.instrumentIds) {
      if (arrangementInstrumentIds.has(instId)) {
        const list = result.byInstrument.get(instId) ?? [];
        list.push(f);
        result.byInstrument.set(instId, list);
        categorized = true;
      }
    }

    if (!categorized) result.unlisted.push(f);
  }
  return result;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const BROWSER_AUDIO_EXTENSIONS = ['.mp3', '.wav', '.flac', '.ogg'];

export function isBrowserPlayable(fileName: string): boolean {
  return BROWSER_AUDIO_EXTENSIONS.includes(getExtension(fileName));
}

export const NOTATION_ACCEPT = NOTATION_EXTENSIONS.join(',');
export const RENDERED_SCORE_ACCEPT = RENDERED_SCORE_EXTENSIONS.join(',');
export const PLAYBACK_ACCEPT = PLAYBACK_EXTENSIONS.join(',');
