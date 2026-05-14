import apiClient from './client';
import type { SeasonPublicData, DownloadSection } from '../types/public';

export const publicApi = {
  getSeason: (token: string, password?: string) =>
    apiClient.get<SeasonPublicData>(`/public/seasons/${token}`, {
      headers: password ? { 'X-Share-Password': password } : undefined,
    }).then((r) => r.data),

  downloadZip: async (token: string, section: DownloadSection | null, password?: string) => {
    const params = new URLSearchParams();
    if (section?.scorePartType) params.set('scorePartType', section.scorePartType);
    if (section?.instrumentId != null) params.set('instrumentId', String(section.instrumentId));
    const qs = params.size ? `?${params}` : '';
    const response = await fetch(`/api/public/seasons/${token}/download${qs}`, {
      headers: password ? { 'X-Share-Password': password } : {},
    });
    if (!response.ok) throw new Error('Download failed');
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const match = disposition.match(/filename="([^"]+)"/);
    const fileName = match ? match[1] : (section ? `${section.label}.zip` : 'Season.zip');
    const blob = await response.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(a.href);
  },
};
