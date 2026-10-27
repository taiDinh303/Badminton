import { useMutation, useQueryClient } from "@tanstack/react-query";

export function useExperienceAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (action: () => Promise<unknown>) => action(),
    onSuccess: async () => {
      await Promise.all(
        [
          "experience",
          "club",
          "bookings",
          "booking",
          "reviews",
          "courts",
          "account",
          "profile",
        ].map((key) => queryClient.invalidateQueries({ queryKey: [key] })),
      );
    },
  });
}
