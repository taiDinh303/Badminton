import FeatureHeading from "../../../components/FeatureHeading";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, ChevronLeft, ChevronRight, Inbox, Mail, Phone, RefreshCw } from "lucide-react";
import { request } from "../../../services/http";
import { Button, Callout, Card } from "../../../components/ui";

type Inquiry = { id: number; fullName: string; phone: string; email: string | null; message: string; createdAt: string; handledAt: string | null };

export default function ContactsPage() {
  const [page, setPage] = useState(1);
  const cache = useQueryClient();
  const query = useQuery({ queryKey: ["contacts", page], queryFn: () => request<{ items: Inquiry[]; totalCount: number }>("GET", "/contacts", undefined, { page }) });
  const handle = useMutation({ mutationFn: (id: number) => request("PATCH", `/contacts/${id}/handled`), onSuccess: () => cache.invalidateQueries({ queryKey: ["contacts"] }) });
  return <div className="space-y-5">
    <FeatureHeading icon={Inbox} title="Liên hệ" subtitle={`${query.data?.totalCount ?? 0} lời nhắn`} actions={<Button variant="ghost" onClick={() => query.refetch()} aria-label="Tải lại"><RefreshCw size={18} /></Button>} />
    {(query.isError || handle.isError) && <Callout tone="error">Không thể xử lý dữ liệu. Vui lòng thử lại.</Callout>}
    {query.isPending && <p role="status" className="p-6 text-muted">Đang tải liên hệ…</p>}
    {query.data?.items.length === 0 && <Card className="flex flex-col items-center gap-3 p-12 text-muted"><Inbox size={36} /><p>Chưa có lời nhắn.</p></Card>}
    <div className="grid gap-4 xl:grid-cols-2">{query.data?.items.map(item => <Card key={item.id} className="p-5">
      <div className="flex items-start justify-between gap-3"><div><h2 className="font-semibold">{item.fullName}</h2><time className="text-xs text-muted">{new Date(item.createdAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</time></div><span className={`rounded-full px-3 py-1 text-xs ${item.handledAt ? "bg-bg text-muted" : "bg-primary/10 text-primary"}`}>{item.handledAt ? "Đã xử lý" : "Mới"}</span></div>
      <div className="my-4 flex flex-wrap gap-3 text-sm"><a className="flex items-center gap-2 text-primary" href={`tel:${item.phone}`}><Phone size={16} />{item.phone}</a>{item.email && <a className="flex min-w-0 items-center gap-2 break-all text-primary" href={`mailto:${item.email}`}><Mail size={16} />{item.email}</a>}</div>
      <p className="mb-5 whitespace-pre-wrap break-words text-sm text-muted">{item.message || "Khách cần tư vấn đặt sân."}</p>
      {!item.handledAt && <Button variant="ghost" disabled={handle.isPending} onClick={() => handle.mutate(item.id)}><Check size={16} /> Đánh dấu đã xử lý</Button>}
    </Card>)}</div>
    {query.data && query.data.totalCount > 20 && <div className="flex items-center justify-center gap-4"><Button variant="ghost" disabled={page === 1} onClick={() => setPage(p => p - 1)} aria-label="Trang trước"><ChevronLeft size={18} /></Button><span className="text-sm">{page} / {Math.ceil(query.data.totalCount / 20)}</span><Button variant="ghost" disabled={page * 20 >= query.data.totalCount} onClick={() => setPage(p => p + 1)} aria-label="Trang sau"><ChevronRight size={18} /></Button></div>}
  </div>;
}
