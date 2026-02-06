import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../api/auth';
import type { LoginRequest } from '../types';

interface AuthState {
  token: string | null;
  refreshToken: string | null;
  userId: string | null;
  username: string | null;
  roles: string[];
}

interface AuthContextValue extends AuthState {
  isAuthenticated: boolean;
  login: (data: LoginRequest) => Promise<void>;
  logout: () => void;
  isAdmin: () => boolean;
  isEditor: () => boolean;
  canEdit: () => boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

function parseJwtRoles(token: string): string[] {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    const roleClaim = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || [];
    return Array.isArray(roleClaim) ? roleClaim : [roleClaim];
  } catch {
    return [];
  }
}

function loadAuthState(): AuthState {
  const token = localStorage.getItem('token');
  const refreshToken = localStorage.getItem('refreshToken');
  const userId = localStorage.getItem('userId');
  const username = localStorage.getItem('username');
  const roles = token ? parseJwtRoles(token) : [];
  return { token, refreshToken, userId, username, roles };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadAuthState);

  // Sync roles when token changes
  useEffect(() => {
    if (state.token) {
      const roles = parseJwtRoles(state.token);
      setState((prev) => ({ ...prev, roles }));
    }
  }, [state.token]);

  const login = useCallback(async (data: LoginRequest) => {
    const response = await authApi.login(data);
    if (!response.success || !response.token || !response.refreshToken) {
      throw new Error(response.message || 'Login failed');
    }
    localStorage.setItem('token', response.token);
    localStorage.setItem('refreshToken', response.refreshToken);
    localStorage.setItem('userId', response.userId || '');
    localStorage.setItem('username', response.username || '');
    setState({
      token: response.token,
      refreshToken: response.refreshToken,
      userId: response.userId || null,
      username: response.username || null,
      roles: parseJwtRoles(response.token),
    });
  }, []);

  const logout = useCallback(() => {
    const rt = localStorage.getItem('refreshToken');
    if (rt) {
      authApi.revokeToken({ refreshToken: rt }).catch(() => {});
    }
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('userId');
    localStorage.removeItem('username');
    setState({ token: null, refreshToken: null, userId: null, username: null, roles: [] });
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    ...state,
    isAuthenticated: !!state.token,
    login,
    logout,
    isAdmin: () => state.roles.includes('Admin'),
    isEditor: () => state.roles.includes('Editor'),
    canEdit: () => state.roles.includes('Admin') || state.roles.includes('Editor'),
  }), [state, login, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
