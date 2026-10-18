import { create } from "zustand";
import { api, type LoginResponse, TOKEN_KEY } from "../lib/api";

export interface AuthUser {
  id: number;
  fullName: string;
  role: string;
  phone: string;
}

interface AuthState {
  user: AuthUser | null;
  login: (phone: string, password: string) => Promise<void>;
  register: (b: { fullName: string; phone: string; password: string }) => Promise<void>;
  logout: () => void;
}

function hydrate(): AuthUser | null {
  try {
    const raw = sessionStorage.getItem("bb_user");
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
}

function persist(user: AuthUser) {
  sessionStorage.setItem("bb_user", JSON.stringify(user));
}

export const useAuth = create<AuthState>((set) => ({
  user: hydrate(),
  login: async (phone, password) => {
    const r: LoginResponse = await api.auth.login({ phoneNumber: phone, password });
    localStorage.setItem(TOKEN_KEY, r.accessToken);
    const u: AuthUser = { id: r.userId, fullName: r.fullName, role: r.role, phone: r.phoneNumber };
    persist(u);
    set({ user: u });
  },
  register: async ({ fullName, phone, password }) => {
    const r: LoginResponse = await api.auth.register({ fullName, phoneNumber: phone, password });
    localStorage.setItem(TOKEN_KEY, r.accessToken);
    const u: AuthUser = { id: r.userId, fullName: r.fullName, role: r.role, phone: r.phoneNumber };
    persist(u);
    set({ user: u });
  },
  logout: () => {
    localStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem("bb_user");
    set({ user: null });
  },
}));

export const isStaff = (u: AuthUser | null) => u?.role === "Manager" || u?.role === "Admin";
export const isAdmin = (u: AuthUser | null) => u?.role === "Admin";
export const isManager = (u: AuthUser | null) => u?.role === "Manager";
