import { create } from "zustand";
import { api, type LoginResponse, TOKEN_KEY } from "../services/api";

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
  updateProfile: (fullName: string) => void;
  logout: () => void;
}

function hydrate(): AuthUser | null {
  try {
    if (!localStorage.getItem(TOKEN_KEY)) return null;
    const raw = sessionStorage.getItem("bb_user");
    const user = raw ? JSON.parse(raw) as AuthUser : null;
    return user && Number.isInteger(user.id) && typeof user.fullName === "string"
      && ["Customer", "Manager", "Admin"].includes(user.role) ? user : null;
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
  updateProfile: (fullName) => set((state) => {
    if (!state.user) return state;
    const user = { ...state.user, fullName }; persist(user); return { user };
  }),
  logout: () => {
    localStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem("bb_user");
    set({ user: null });
  },
}));

export const isStaff = (u: AuthUser | null) => u?.role === "Manager" || u?.role === "Admin";
export const isAdmin = (u: AuthUser | null) => u?.role === "Admin";
export const isManager = (u: AuthUser | null) => u?.role === "Manager";
