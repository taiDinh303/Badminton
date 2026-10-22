import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AnimatePresence } from "framer-motion";
import { Users, Plus, UserPlus, Crown, Copy, Check } from "lucide-react";
import { api, type GroupView, type GroupMemberView } from "../../../services/api";
import {
  Card,
  Callout,
  Busy,
  Button,
  Field,
  Modal,
  EmptyState,
  PageHeader,
  StatusBadge,
} from "../../../components/ui";
import { useAuth } from "../../../store/auth";

/**
 * FEAT 7 — Groups (docs 03 §7): customer creates a group (max 10),
 * joins a group by id, lists members, and books for a group they own.
 */
export default function GroupsPage() {
  const qc = useQueryClient();
  const user = useAuth((s) => s.user);
  const [createOpen, setCreateOpen] = useState(false);
  const [joinOpen, setJoinOpen] = useState(false);
  const [membersOf, setMembersOf] = useState<GroupView | null>(null);

  const [name, setName] = useState("");
  const [maxMembers, setMaxMembers] = useState(10);
  const [joinId, setJoinId] = useState("");
  // Created-group banner: surface the new group ID so the owner can share it.
  const [createdGroup, setCreatedGroup] = useState<GroupView | null>(null);
  const [copied, setCopied] = useState(false);

  const { data: rawGroups = [], isLoading, isError } = useQuery({
    queryKey: ["groups", "mine"],
    queryFn: api.groups.mine,
  });

  // Defensive dedupe by id: each group renders exactly once ("chỉ hiện 1 cái").
  // `mine` is owner-only server-side, so groups created by others never appear.
  const groups = useMemo(() => {
    const seen = new Set<number>();
    return rawGroups.filter((g) => {
      if (seen.has(g.id)) return false;
      seen.add(g.id);
      return true;
    });
  }, [rawGroups]);

  // Hide the "created" banner once the group is part of the list, so it
  // isn't shown twice (banner + card).
  const bannerVisible =
    createdGroup && !rawGroups.some((g) => g.id === createdGroup.id);

  const { data: members } = useQuery({
    queryKey: ["groups", "members", membersOf?.id],
    queryFn: () => api.groups.members(membersOf!.id),
    enabled: membersOf != null,
  });

  const create = useMutation({
    mutationFn: () => api.groups.create(name.trim(), maxMembers),
    onSuccess: (g) => {
      setCreateOpen(false);
      setCreatedGroup(g);
      setName("");
      setMaxMembers(10);
      qc.invalidateQueries({ queryKey: ["groups", "mine"] });
    },
  });

  async function copyGroupId() {
    if (!createdGroup) return;
    try {
      await navigator.clipboard.writeText(String(createdGroup.id));
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      /* clipboard blocked — user can still see the ID on screen */
    }
  }

  const join = useMutation({
    mutationFn: () => api.groups.join(Number(joinId)),
    onSuccess: () => {
      setJoinOpen(false);
      setJoinId("");
      qc.invalidateQueries({ queryKey: ["groups", "mine"] });
    },
  });

  if (isLoading && !groups.length)
    return <div className="p-4"><Busy label="Đang tải nhóm…" /></div>;

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Nhóm chơi"
        subtitle="Tạo nhóm, mời bạn và đặt sân chung (tối đa 10 thành viên)"
        right={
          <div className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={() => setJoinOpen(true)}>
              <UserPlus className="h-4 w-4" /> Tham gia
            </Button>
            <Button variant="accent" size="sm" onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" /> Tạo nhóm
            </Button>
          </div>
        }
      />

      {isError && (
        <Callout tone="error" className="mt-4">Không tải được nhóm.</Callout>
      )}

      {/* Created-group banner: show the new group's ID so it can be shared.
          Only until it lands in the list (avoids showing the group twice). */}
      {bannerVisible && createdGroup && (
        <div className="mt-4">
          <Callout tone="success">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span>
                Nhóm <strong>{createdGroup.name}</strong> đã được tạo.
                Chia sẻ <strong>ID {createdGroup.id}</strong> cho bạn bè để tham gia.
              </span>
              <Button
                variant="ghost"
                size="xs"
                onClick={copyGroupId}
                aria-label="Sao chép ID nhóm"
              >
                {copied ? (
                  <Check className="h-3.5 w-3.5 text-state-success" />
                ) : (
                  <Copy className="h-3.5 w-3.5" />
                )}
                {copied ? "Đã copy" : "Copy ID"}
              </Button>
            </div>
          </Callout>
        </div>
      )}

      {groups.length === 0 ? (
        <div className="mt-6">
          <EmptyState
            icon={<Users className="h-5 w-5" />}
            title="Chưa có nhóm nào"
            hint="Tạo nhóm để đặt sân chung với bạn bè, hoặc tham gia một nhóm có sẵn."
          >
            <Button variant="accent" size="sm" onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" /> Tạo nhóm đầu tiên
            </Button>
          </EmptyState>
        </div>
      ) : (
        <div className="mt-5 grid gap-3 sm:grid-cols-2">
          {groups.map((g) => (
            <Card key={g.id} className="cursor-pointer transition-colors hover:border-primary">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <Users className="h-5 w-5" />
                  </span>
                  <div>
                    <p className="font-semibold">{g.name}</p>
                    <p className="text-xs text-muted">
                      {g.memberCount}/{g.maxMembers} thành viên
                    </p>
                  </div>
                </div>
                <StatusBadge status={g.memberCount < g.maxMembers ? "Open" : "Locked"} size="sm" />
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border pt-3">
                <Button variant="ghost" size="xs" onClick={() => setMembersOf(g)}>
                  Xem thành viên
                </Button>
                {g.ownerId === user?.id ? (
                  <div className="flex items-center gap-2">
                    <p className="flex items-center gap-1 text-xs text-muted">
                      <Crown className="h-3.5 w-3.5 text-accent" /> Chủ nhóm
                    </p>
                    <span className="inline-flex items-center gap-1 rounded-full bg-bg px-2 py-0.5 text-[11px] font-semibold text-muted tnum">
                      ID {g.id}
                    </span>
                  </div>
                ) : null}
              </div>
            </Card>
          ))}
        </div>
      )}

      {/* Create modal */}
      {createOpen && (
        <Modal
          title="Tạo nhóm mới"
          onClose={() => setCreateOpen(false)}
          width="max-w-sm"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => setCreateOpen(false)}>
                Hủy
              </Button>
              <Button
                variant="accent"
                size="sm"
                disabled={create.isPending || !name.trim()}
                onClick={() => create.mutate()}
              >
                {create.isPending ? "Đang tạo…" : "Tạo nhóm"}
              </Button>
            </>
          }
        >
          <div className="space-y-3">
            <Field
              label="Tên nhóm"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="VD: Đội Cầu Lông Văn Phòng"
              required
            />
            <div>
              <p className="mb-1.5 text-sm font-medium text-muted">Số thành viên tối đa</p>
              <div
                className="grid grid-cols-5 gap-1.5"
                role="radiogroup"
                aria-label="Số thành viên tối đa"
              >
                {[2, 4, 6, 8, 10].map((n) => (
                  <button
                    key={n}
                    type="button"
                    role="radio"
                    aria-checked={maxMembers === n}
                    onClick={() => setMaxMembers(n)}
                    className={
                      "h-10 rounded-lg border text-sm font-semibold transition-colors cursor-pointer " +
                      (maxMembers === n
                        ? "border-primary bg-primary/10 text-primary"
                        : "border-border bg-card text-muted hover:border-primary/40")
                    }
                  >
                    {n}
                  </button>
                ))}
              </div>
              <p className="mt-1.5 text-[11px] text-muted">Mỗi nhóm tối đa 10 thành viên (docs 03 §7).</p>
            </div>
            {create.isError && (
              <Callout tone="error">
                {create.error instanceof Error ? create.error.message : "Không tạo được."}
              </Callout>
            )}
          </div>
        </Modal>
      )}

      {/* Join modal */}
      {joinOpen && (
        <Modal
          title="Tham gia nhóm"
          onClose={() => setJoinOpen(false)}
          width="max-w-sm"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => setJoinOpen(false)}>
                Hủy
              </Button>
              <Button
                variant="accent"
                size="sm"
                disabled={join.isPending || !joinId.trim()}
                onClick={() => join.mutate()}
              >
                {join.isPending ? "Đang tham gia…" : "Tham gia"}
              </Button>
            </>
          }
        >
          <div className="space-y-2">
            <p className="text-sm text-muted">
              Nhập ID nhóm do chủ nhóm chia sẻ (VD: 3).
            </p>
            <Field
              label="ID nhóm"
              inputMode="numeric"
              pattern="[0-9]*"
              value={joinId}
              onChange={(e) => setJoinId(e.target.value.replace(/[^0-9]/g, ""))}
              placeholder="VD: 3"
            />
            {join.isError && (
              <Callout tone="error">
                {join.error instanceof Error ? join.error.message : "Không tham gia được."}
              </Callout>
            )}
          </div>
        </Modal>
      )}

      {/* Members modal */}
      <AnimatePresence>
        {membersOf && (
          <Modal
            title={`Thành viên · ${membersOf.name}`}
            onClose={() => setMembersOf(null)}
            width="max-w-md"
          >
            <div className="space-y-2">
              {members?.map((m: GroupMemberView) => (
                <div
                  key={m.userId}
                  className="flex items-center gap-3 rounded-lg border border-border bg-bg/40 p-2.5"
                >
                  <span className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                    {m.fullName.charAt(0).toUpperCase()}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="flex items-center gap-1 text-sm font-medium">
                      {m.fullName}
                      {m.isCreator && (
                        <Crown className="h-3.5 w-3.5 text-accent" aria-label="Chủ nhóm" />
                      )}
                    </p>
                    <p className="text-[11px] text-muted tnum">{m.phoneNumber}</p>
                  </div>
                  <span className="text-[11px] text-muted">
                    {new Date(m.joinedAt).toLocaleDateString("vi-VN")}
                  </span>
                </div>
              ))}
              {!members && <p className="text-sm text-muted">Đang tải…</p>}
            </div>
          </Modal>
        )}
      </AnimatePresence>
    </div>
  );
}
