import { useEffect, type ReactNode } from "react";
import { QueryClientProvider } from "@tanstack/react-query";
import { useAuth } from "../store/auth";
import { queryClient } from "./queryClient";

export default function Providers({ children }: { children: ReactNode }) {
  useEffect(() => {
    const unsubscribe = useAuth.subscribe((state, previous) => {
      if (state.user !== previous.user) {
        void queryClient.cancelQueries();
        queryClient.clear();
      }
    });
    const expire = () => useAuth.getState().logout();
    window.addEventListener("badminton:session-expired", expire);
    return () => { unsubscribe(); window.removeEventListener("badminton:session-expired", expire); };
  }, []);
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
