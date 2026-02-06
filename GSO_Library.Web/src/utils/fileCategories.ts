import type { ArrangementFile } from '../types';

const ARRANGEMENT_EXTENSIONS = ['.xml', '.mxl', '.mscz', '.dorico', '.sib'];
const PDF_EXTENSIONS = ['.pdf'];
const PLAYBACK_EXTENSIONS = ['.mid', '.midi', '.mp3', '.wav', '.flac', '.ogg'];

export interface CategorizedFiles {
  arrangementFiles: ArrangementFile[];
  pdfFiles: ArrangementFile[];
  playbackFiles: ArrangementFile[];
}

function getExtension(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot >= 0 ? fileName.slice(dot).toLowerCase() : '';
}

export function categorizeFiles(files: ArrangementFile[]): CategorizedFiles {
  const arrangementFiles: ArrangementFile[] = [];
  const pdfFiles: ArrangementFile[] = [];
  const playbackFiles: ArrangementFile[] = [];

  for (const file of files) {
    const ext = getExtension(file.fileName);
    if (ARRANGEMENT_EXTENSIONS.includes(ext)) {
      arrangementFiles.push(file);
    } else if (PDF_EXTENSIONS.includes(ext)) {
      pdfFiles.push(file);
    } else if (PLAYBACK_EXTENSIONS.includes(ext)) {
      playbackFiles.push(file);
    } else {
      // Uncategorized files go into arrangement files as a fallback
      arrangementFiles.push(file);
    }
  }

  return { arrangementFiles, pdfFiles, playbackFiles };
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export const ARRANGEMENT_ACCEPT = ARRANGEMENT_EXTENSIONS.join(',');
export const PDF_ACCEPT = PDF_EXTENSIONS.join(',');
export const PLAYBACK_ACCEPT = PLAYBACK_EXTENSIONS.join(',');
