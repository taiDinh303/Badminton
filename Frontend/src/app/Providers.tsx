import { useEffect, type ReactNode } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { useAuth } from "../store/auth";
import { queryClient } from "./queryClient";
import { warmSession } from "./warmSession";
import { TOKEN_KEY, expireLocalSession, sessionMatchesStorage } from "../services/session";

export default function Providers({ children }: { children: ReactNode }) {
  useEffect(() => {
    let stop = useAuth.getState().user ? warmSession(queryClient, useAuth.getState().user!) : undefined;
    const unsubscribe = useAuth.subscribe((state, previous) => {
      if (state.user?.id !== previous.user?.id || state.user?.role !== previous.user?.role) {
        stop?.();
        void queryClient.cancelQueries();
        queryClient.clear();
        stop = state.user ? warmSession(queryClient, state.user) : undefined;
      }
    });
    const expire = () => useAuth.getState().logout();
    window.addEventListener("badminton:session-expired", expire);
    const syncSession = (event: StorageEvent) => {
      if (event.storageArea === localStorage && (event.key === TOKEN_KEY || event.key === null) && !sessionMatchesStorage())
        expireLocalSession();
    };
    window.addEventListener("storage", syncSession);
    return () => { stop?.(); unsubscribe(); window.removeEventListener("badminton:session-expired", expire); window.removeEventListener("storage", syncSession); };
  }, []);
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
