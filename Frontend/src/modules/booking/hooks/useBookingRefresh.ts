import { useQueryClient } from "@tanstack/react-query";

/** Booking state changes affect inventory, receipts, reports and the owner's balance. */
export function useBookingRefresh() {
  const client = useQueryClient();
  return () => {
    for (const key of ["booking", "my-bookings", "admin-bookings", "payments", "account", "availability", "report", "booking-quote"])
      void client.invalidateQueries({ queryKey: [key] });
  };
}
