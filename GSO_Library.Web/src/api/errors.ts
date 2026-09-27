import axios from 'axios';

export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error) && typeof error.response?.data === 'string' && error.response.data) {
    return error.response.data;
  }
  return fallback;
}
