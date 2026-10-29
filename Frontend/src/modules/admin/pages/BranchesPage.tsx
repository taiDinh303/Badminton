import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Branch } from "../../../services/api";
import { Toolbar, Busy, Callout, Button, Field, Modal } from "../../../components/ui";

type Form = {
  id?: number;
  name: string;
  address: string;
  openTime: string;
  closeTime: string;
  isActive: boolean;
  geoLat: string;
  geoLng: string;
};

const empty = (): Form => ({
  name: "",
  address: "",
  openTime: "06:00",
  closeTime: "23:00",
  isActive: true,
  geoLat: "",
  geoLng: "",
});

export function branchMapsUrl(lat?: number | null, lng?: number | null): string | null {
  if (lat == null || lng == null || !Number.isFinite(lat) || !Number.isFinite(lng)) return null;
  return `https://www.google.com/maps?q=${lat},${lng}&z=15&output=embed`;
}

export function branchDirectionsUrl(lat?: number | null, lng?: number | null): string | null {
  if (lat == null || lng == null || !Number.isFinite(lat) || !Number.isFinite(lng)) return null;
  return `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(`${lat},${lng}`)}`;
}

export default function BranchesPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState<Form | null>(null);
  const impact = useQuery({
    queryKey: ["branch-impact", form?.id],
    queryFn: () => api.branches.impact(form!.id!),
    enabled: form?.id != null,
  });
  const branches = useQuery({ queryKey: ["branches"], queryFn: api.branches.list });

  const geoParsed = useMemo(() => {
    if (!form) return { lat: null as number | null, lng: null as number | null, error: null as string | null };
    const hasLat = form.geoLat.trim() !== "";
    const hasLng = form.geoLng.trim() !== "";
    if (hasLat !== hasLng)
      return { lat: null, lng: null, error: "Nhập cả Vĩ độ và Kinh độ, hoặc bỏ trống cả hai để xóa vị trí." };
    if (!hasLat && !hasLng) return { lat: null, lng: null, error: null };
    const lat = Number(form.geoLat);
    const lng = Number(form.geoLng);
    if (!Number.isFinite(lat) || !Number.isFinite(lng))
      return { lat: null, lng: null, error: "Vĩ độ/Kinh độ phải là số." };
    if (lat < -90 || lat > 90)
      return { lat: null, lng: null, error: "Vĩ độ phải trong khoảng -90 đến 90." };
    if (lng < -180 || lng > 180)
      return { lat: null, lng: null, error: "Kinh độ phải trong khoảng -180 đến 180." };
    return { lat, lng, error: null };
  }, [form]);

  const save = useMutation({
    mutationFn: async (f: Form) => {
      const hasGeo = f.geoLat.trim() !== "" || f.geoLng.trim() !== "";
      const geoPayload = hasGeo
        ? { geoLat: geoParsed.lat, geoLng: geoParsed.lng, hasGeo: true }
        : {};
      if (f.id === undefined) {
        return api.branches.create({
          name: f.name.trim(),
          address: f.address.trim(),
          openTime: f.openTime,
          closeTime: f.closeTime,
          ...(geoPayload as object),
        });
      }
      return api.branches.update(f.id, {
        name: f.name.trim(),
        address: f.address.trim(),
        openTime: f.openTime,
        closeTime: f.closeTime,
        isActive: f.isActive,
        ...(geoPayload as object),
      });
    },
    onSuccess: () => {
      setForm(null);
      void qc.invalidateQueries({ queryKey: ["branches"] });
      void qc.invalidateQueries({ queryKey: ["availability"] });
    },
  });

  const edit = (branch: Branch) => {
    save.reset();
    setForm({
      id: branch.id,
      name: branch.name,
      address: branch.address,
      openTime: branch.openTime,
      closeTime: branch.closeTime,
      isActive: branch.isActive,
      geoLat: branch.geoLat == null ? "" : String(branch.geoLat),
      geoLng: branch.geoLng == null ? "" : String(branch.geoLng),
    });
  };

  const formMap = branchMapsUrl(geoParsed.lat, geoParsed.lng);
  const formDirections = branchDirectionsUrl(geoParsed.lat, geoParsed.lng);
  const submitDisabled =
    save.isPending ||
    !form ||
    !form.name.trim() ||
    !form.address.trim() ||
    !form.openTime ||
    form.closeTime <= form.openTime ||
    geoParsed.error != null;

  return (
    <div>
      <Toolbar
        title="Cơ sở"
        subtitle="Quản lý địa chỉ, vị trí bản đồ, giờ mở cửa và trạng thái hoạt động"
        actions={
          <Button onClick={() => { save.reset(); setForm(empty()); }}>Thêm cơ sở</Button>
        }
      />
      {branches.isPending && <Busy />}
      {branches.isError && <Callout tone="error">{branches.error.message}</Callout>}
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        {branches.data?.map((b) => {
          const mapUrl = branchMapsUrl(b.geoLat, b.geoLng);
          const dirUrl = branchDirectionsUrl(b.geoLat, b.geoLng);
          return (
            <div key={b.id} className="rounded-xl border border-border bg-card p-4">
              <h2 className="font-bold">{b.name}</h2>
              <p className="mt-1 text-sm text-muted">{b.address}</p>
              <p className="my-2 text-sm">
                {b.openTime} – {b.closeTime} · {b.isActive ? "Đang hoạt động" : "Tạm ngưng"}
              </p>
              {mapUrl && (
                <div className="mt-3 overflow-hidden rounded-lg border border-border">
                  <iframe
                    title={`Bản đồ ${b.name}`}
                    src={mapUrl}
                    className="h-44 w-full border-0"
                    loading="lazy"
                    referrerPolicy="no-referrer-when-downgrade"
                  />
                </div>
              )}
              <div className="mt-2 flex items-center gap-3">
                <Button size="sm" variant="ghost" onClick={() => edit(b)}>
                  Sửa {b.name}
                </Button>
                {dirUrl && (
                  <a
                    href={dirUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-sm font-medium text-primary hover:underline"
                  >
                    Chỉ đường
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {form && (
        <Modal
          title={form.id === undefined ? "Thêm cơ sở" : "Sửa cơ sở"}
          onClose={() => setForm(null)}
          footer={
            <Button disabled={submitDisabled} onClick={() => save.mutate(form)}>
              {save.isPending ? "Đang lưu…" : "Lưu cơ sở"}
            </Button>
          }
        >
          <div className="grid gap-3">
            {form.id != null && impact.data && (
              <Callout tone="info">
                {impact.data.count} đơn đang hoạt động
                {impact.data.bookingCodes.length > 0
                  ? `: ${impact.data.bookingCodes.join(", ")}`
                  : ""}
                . Đóng cơ sở hoặc thu hẹp giờ sẽ bị chặn nếu ảnh hưởng các đơn này.
              </Callout>
            )}
            {impact.isError && (
              <Callout tone="error">
                Không tải được danh sách đơn bị ảnh hưởng. {impact.error.message}
              </Callout>
            )}
            <Field label="Tên cơ sở" maxLength={100} value={form.name}
              onChange={e => setForm({ ...form, name: e.target.value })} />
            <Field label="Địa chỉ" maxLength={255} value={form.address}
              onChange={e => setForm({ ...form, address: e.target.value })} />
            <Field label="Giờ mở cửa" type="time" value={form.openTime}
              onChange={e => setForm({ ...form, openTime: e.target.value })} />
            <Field label="Giờ đóng cửa" type="time" value={form.closeTime}
              onChange={e => setForm({ ...form, closeTime: e.target.value })} />
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Vĩ độ (latitude)" hint="Vd: 21.0245" value={form.geoLat}
                onChange={e => setForm({ ...form, geoLat: e.target.value })} />
              <Field label="Kinh độ (longitude)" hint="Vd: 105.8412" value={form.geoLng}
                onChange={e => setForm({ ...form, geoLng: e.target.value })} />
            </div>
            {geoParsed.error && <Callout tone="error">{geoParsed.error}</Callout>}
            {formMap && (
              <div className="overflow-hidden rounded-lg border border-border">
                <iframe
                  title="Xem trước vị trí"
                  src={formMap}
                  className="h-52 w-full border-0"
                  loading="lazy"
                  referrerPolicy="no-referrer-when-downgrade"
                />
              </div>
            )}
            {formDirections && (
              <a
                href={formDirections}
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm font-medium text-primary hover:underline"
              >
                Mở chỉ đường cho vị trí này
              </a>
            )}
            {form.id !== undefined && (
              <label className="flex gap-2 text-sm">
                <input type="checkbox" checked={form.isActive}
                  onChange={e => setForm({ ...form, isActive: e.target.checked })} />
                Đang hoạt động
              </label>
            )}
            {save.isError && <Callout tone="error">{save.error.message}</Callout>}
          </div>
        </Modal>
      )}
    </div>
  );
}
