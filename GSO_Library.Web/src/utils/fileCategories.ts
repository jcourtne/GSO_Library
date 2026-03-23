import type { ArrangementFile } from '../types';

const NOTATION_EXTENSIONS = ['.xml', '.mxl', '.mscz', '.dorico', '.sib'];
const RENDERED_SCORE_EXTENSIONS = ['.pdf', '.zip'];
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
