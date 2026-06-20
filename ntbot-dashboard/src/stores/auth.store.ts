import { create } from 'zustand';
import type { User, Tenant } from '../types';

interface AuthState {
  isAuthenticated: boolean;
  user: User | null;
  tenant: Tenant | null;
  token: string | null;
  login: (user: User, tenant: Tenant, token: string) => void;
  logout: () => void;
  updateUser: (user: Partial<User>) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  isAuthenticated: false,
  user: null,
  tenant: null,
  token: null,

  login: (user, tenant, token) => {
    set({
      isAuthenticated: true,
      user,
      tenant,
      token,
    });
  },

  logout: () => {
    set({
      isAuthenticated: false,
      user: null,
      tenant: null,
      token: null,
    });
  },

  updateUser: (userUpdate) => {
    set((state) => ({
      user: state.user ? { ...state.user, ...userUpdate } : null,
    }));
  },
}));
