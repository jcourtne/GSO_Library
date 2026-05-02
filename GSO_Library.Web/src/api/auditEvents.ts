import apiClient from './client';
import type { AuditEvent } from '../types';

export const auditEventsApi = {
  list: (params: { eventTypes?: string[]; usernames?: string[]; from?: string; to?: string }) => {
    const searchParams = new URLSearchParams();
    params.eventTypes?.forEach((t) => searchParams.append('eventTypes', t));
    params.usernames?.forEach((u) => searchParams.append('usernames', u));
    if (params.from) searchParams.set('from', params.from);
    if (params.to) searchParams.set('to', params.to);
    return apiClient.get<AuditEvent[]>(`/audit-events?${searchParams}`).then((r) => r.data);
  },

  usernames: () =>
    apiClient.get<string[]>('/audit-events/usernames').then((r) => r.data),
};
