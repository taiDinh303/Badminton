import { Link } from "react-router-dom";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../../services/api";
import { Card, Button, Callout, Busy } from "../../../components/ui";
import { formatVnd, formatDay } from "../../../utils/format";

const reasons: Record<string, string> = {
  initialization_uncertain: "Khởi tạo chưa rõ kết quả", late_receipt: "Tiền về khi đơn đã hết hạn/hủy",
  additional_receipt: "Có thêm giao dịch cho đơn đã thanh toán", confirmation_conflict: "Tiền đã về nhưng xác nhận đơn bị xung đột",
  refund_uncertain: "Đang chờ xác minh kết quả hoàn tiền",
};

export default function ReconciliationPage() {
  const [page, setPage] = useState(1);
  const client = useQueryClient();
  const refund = useMutation({ mutationFn: api.payments.reconcileRefund, onSuccess: () => {
    for (const key of ["payment-reconciliation", "booking", "payments", "account", "report", "availability"])
      void client.invalidateQueries({ queryKey: [key] });
  } });
  const query = useQuery({ queryKey: ["payment-reconciliation", page], queryFn: () => api.payments.reconciliation(page) });
  return <div className="mx-auto max-w-4xl p-4">
    <h1 className="text-xl font-bold">Đối soát thanh toán</h1>
    <p className="mt-2 text-sm text-muted">Các giao dịch cần kiểm tra với cổng thanh toán trước khi xác nhận hoặc hoàn tiền. Danh sách giới hạn theo quyền cơ sở.</p>
    <Button className="my-4" disabled={query.isFetching} onClick={() => query.refetch()}>Tải lại</Button>
    {query.isLoading && <Busy />}
    {query.isError && <Callout tone="error">{query.error.message}</Callout>}
    {refund.isError && <Callout tone="error">{refund.error.message}</Callout>}
    {refund.isSuccess && <Callout tone="success">Đã xác nhận hoàn tiền với provider.</Callout>}
    {query.data?.items.length === 0 && <Card className="p-5">Không có giao dịch cần đối soát.</Card>}
    <div className="space-y-3">{query.data?.items.map(({ payment: p, reason, createdAt }) => <Card key={p.id} className="min-w-0 p-4">
      <Link className="font-semibold underline" to={`/bookings/${p.bookingId}`}>Đơn #{p.bookingId}</Link>
      <p className="mt-2 text-sm">{p.method} · {formatVnd(p.amount)} · {p.status} · {formatDay(createdAt)}</p>
      <p className="mt-1 break-all text-sm text-muted">Mã giao dịch: {p.gatewayRef ?? "—"}</p>
      <Callout tone="warning" className="mt-2">{reasons[reason ?? ""] ?? "Cần kiểm tra giao dịch"}</Callout>
      {p.status === "Success" && reason && ["late_receipt", "additional_receipt", "confirmation_conflict", "refund_uncertain"].includes(reason) &&
        <Button className="mt-3" disabled={refund.isPending} onClick={() => {
          if (reason === "refund_uncertain" || window.confirm(`Hoàn ${formatVnd(p.amount)} của giao dịch #${p.id} qua cổng thanh toán?`)) refund.mutate(p.id);
        }}>{reason === "refund_uncertain" ? "Truy vấn kết quả hoàn tiền" : "Hoàn khoản thu này"}</Button>}
    </Card>)}</div>
    <div className="mt-4 flex items-center gap-3">
      <Button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Trước</Button><span>Trang {page}</span>
      <Button disabled={!query.data || page * 50 >= query.data.totalCount} onClick={() => setPage(p => p + 1)}>Sau</Button>
    </div>
  </div>;
}
