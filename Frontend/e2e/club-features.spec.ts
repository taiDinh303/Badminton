import { test, expect } from "./fixtures";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";
import type { Page } from "@playwright/test";
type Handler = Parameters<typeof mockApi>[1][number];
const branch = { id: 1, name: "Cơ sở A", address: "Thủ Đức", isActive: true };
const item = {
  id: 11,
  branchId: 1,
  name: "Vợt A",
  rental: true,
  stock: 2,
  available: 2,
  unitPrice: 20000,
  active: true,
};
const coach = {
  id: 21,
  branchId: 1,
  name: "HLV Lan",
  bio: "Đánh đôi",
  active: true,
};
const slot = {
  courtReady: true,
  courtBookingId: 77,
  id: 31,
  coachId: 21,
  startsAtUtc: "2026-11-02T03:00:00Z",
  endsAtUtc: "2026-11-02T04:00:00Z",
  capacity: 4,
  available: 4,
  pricePerPerson: 100000,
  cancelled: false,
};
test("attendance split previews exact VND and submits recorded minutes", async ({
  page,
}) => {
  let payload: unknown;
  await setup(page, [
    {
      pattern: /\/groups\/joined$/,
      respond: () => ok([{ id: 7, name: "Nhóm A", ownerId: 1 }]),
    },
    {
      pattern: /\/groups\/7\/members$/,
      respond: () =>
        ok([
          { userId: 1, fullName: "Alice" },
          { userId: 2, fullName: "Bob" },
        ]),
    },
    {
      pattern: /\/club\/expenses\/bookings\?/,
      respond: () =>
        ok([
          {
            id: 91,
            code: "BB91",
            date: "2026-11-02",
            status: "Confirmed",
            payableAmount: 100001,
          },
        ]),
    },
    {
      pattern: /\/club\/expenses\/91$/,
      method: "GET",
      respond: () => ok(null),
    },
    {
      pattern: /\/club\/expenses\/91\/attendance$/,
      method: "POST",
      respond: (_r, _u, body) => {
        payload = body;
        return ok("saved");
      },
    },
  ]);
  await page.goto("/group-expenses?groupId=7&bookingId=91");
  await page.getByLabel("Alice", { exact: true }).check();
  await page.getByLabel("Bob", { exact: true }).check();
  await page.getByLabel("Chia theo phút tham dự").check();
  await page.getByLabel("Phút tham dự Alice").fill("30");
  await page.getByLabel("Phút tham dự Bob").fill("60");
  await expect(page.getByText(/Alice: dự kiến 33\.334/)).toBeVisible();
  await expect(page.getByText(/Bob: dự kiến 66\.667/)).toBeVisible();
  await page.getByRole("button", { name: "Chốt theo thời lượng" }).click();
  await expect
    .poll(() => payload)
    .toEqual({
      members: [
        { userId: 1, minutes: 30 },
        { userId: 2, minutes: 60 },
      ],
    });
});
test("staff binds an unavailable coaching draft to a confirmed court", async ({
  page,
}) => {
  let bound = false;
  await setup(
    page,
    [
      {
        pattern: /\/club\/coaches\/court-bookings\?/,
        respond: () =>
          ok([
            {
              bookingId: 77,
              code: "BB-COURT",
              court: "Sân A",
              startsAtUtc: slot.startsAtUtc,
              endsAtUtc: slot.endsAtUtc,
            },
          ]),
      },
      {
        pattern: /\/club\/coaches\/21\/slots\?/,
        respond: () =>
          ok([
            {
              ...slot,
              courtReady: bound,
              courtBookingId: bound ? 77 : null,
              available: bound ? 4 : 0,
            },
          ]),
      },
      {
        pattern: /\/club\/coaches\/slots\/31\/court$/,
        method: "PUT",
        respond: (_r, _u, body) => {
          bound = body === 77;
          return ok("saved");
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/coaches");
  await page.getByLabel("Huấn luyện viên", { exact: true }).selectOption("21");
  await expect(
    page.getByText("Chưa có sân hợp lệ · Tạm ngừng nhận học viên"),
  ).toBeVisible();
  await page.locator('select[name="courtBooking"]').selectOption("77");
  await page.getByRole("button", { name: "Gắn sân cho lớp #31" }).click();
  await expect(page.getByText("Đã giữ sân · Đơn #77")).toBeVisible();
});
test("damage report records a reason without pretending the assessment was collected", async ({
  page,
}) => {
  let report: {
    unusableQuantity: number;
    lost: boolean;
    assessedAmount: number;
    reason: string;
  } | null = null;
  await setup(
    page,
    [
      {
        pattern: /\/club\/equipment\/orders\?/,
        respond: () =>
          ok([
            {
              id: 51,
              userId: 2,
              branchId: 1,
              item: "Vợt A",
              rental: true,
              quantity: 2,
              total: 40000,
              status: report ? "Lost" : "Issued",
              dueAtUtc: slot.endsAtUtc,
              returnedAtUtc: null,
              overdue: true,
            },
          ]),
      },
      {
        pattern: /\/club\/equipment\/orders\/51\/condition$/,
        method: "GET",
        respond: () =>
          ok(report ? { ...report, actorId: 1, at: slot.endsAtUtc } : null),
      },
      {
        pattern: /\/club\/equipment\/orders\/51\/condition$/,
        method: "POST",
        respond: (_r, _u, body) => {
          report = body as typeof report;
          return ok("saved");
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/equipment");
  await expect(page.getByText(/Quá hạn trả ·/)).toBeVisible();
  await page.getByText("Biên bản dụng cụ #51", { exact: true }).click();
  await page.getByLabel("Mất dụng cụ", { exact: false }).check();
  await page.getByLabel("Khoản đề nghị đối chiếu (đ)").fill("50000");
  await page
    .getByLabel("Lý do / tình trạng")
    .fill("Khách xác nhận mất một vợt");
  await page.getByRole("button", { name: "Chốt biên bản #51" }).click();
  await expect
    .poll(() => report)
    .toMatchObject({ lost: true, unusableQuantity: 1, assessedAmount: 50000 });
  await expect(page.getByText(/chưa phải tiền đã thu/)).toBeVisible();
});
test("score correction shows audit history and submits a reason with the viewed revision", async ({
  page,
}) => {
  let payload: unknown;
  await setup(
    page,
    [
      {
        pattern: /\/club\/tournaments\/41$/,
        respond: () =>
          ok({
            tournament: { ...tournament, status: "Completed" },
            teams: [
              {
                id: 1,
                name: "A",
                players: "Alice",
                pool: 1,
                played: 1,
                won: 1,
                pointsFor: 21,
                pointsAgainst: 15,
              },
              {
                id: 2,
                name: "B",
                players: "Bob",
                pool: 1,
                played: 1,
                won: 0,
                pointsFor: 15,
                pointsAgainst: 21,
              },
            ],
            matches: [
              {
                id: 71,
                teamAId: 1,
                teamBId: 2,
                pool: 1,
                round: 1,
                startsAtUtc: slot.startsAtUtc,
                endsAtUtc: slot.endsAtUtc,
                location: "Sân A",
                scoreA: 21,
                scoreB: 15,
              },
            ],
          }),
      },
      {
        pattern: /\/club\/tournaments\/41\/matches\/71\/history$/,
        respond: () =>
          ok([
            {
              revision: 1,
              actorId: 8,
              oldScoreA: null,
              oldScoreB: null,
              scoreA: 21,
              scoreB: 15,
              reason: "Kết quả lần đầu",
              at: slot.endsAtUtc,
            },
          ]),
      },
      {
        pattern: /\/club\/tournaments\/41\/matches\/71\/score$/,
        method: "PUT",
        respond: (_r, _u, body) => {
          payload = body;
          return err(
            409,
            "CONFLICT",
            "Kết quả đã thay đổi. Tải lại lịch sử trước khi sửa.",
          );
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/tournaments");
  await page.getByLabel("Giải đấu", { exact: true }).selectOption("41");
  await page.getByText("Xếp lịch / ghi kết quả trận #71").click();
  await expect(page.getByText(/Nhân viên #8.*Kết quả lần đầu/)).toBeVisible();
  await page.getByLabel("Lý do ghi / sửa kết quả").fill("Đối chiếu biên bản");
  await page.getByLabel("Điểm đội A").fill("15");
  await page.getByLabel("Điểm đội B").fill("21");
  await page
    .getByRole("button", { name: "Lưu kết quả trận 71", exact: true })
    .click();
  await expect
    .poll(() => payload)
    .toMatchObject({
      scoreA: 15,
      scoreB: 21,
      reason: "Đối chiếu biên bản",
      expectedRevision: 1,
    });
  await expect(
    page.getByText("Kết quả đã thay đổi. Tải lại lịch sử trước khi sửa.", {
      exact: true,
    }),
  ).toBeVisible();
});
const tournament = {
  id: 41,
  branchId: 1,
  organizerId: 8,
  name: "Giải cuối tuần",
  maxTeams: 8,
  status: "Registration",
};
test.describe("Vietnam device timezone", () => {
  test.use({ timezoneId: "Asia/Ho_Chi_Minh" });
  test("SQL Server timestamps without a suffix show the correct Vietnam class time", async ({ page }) => {
    await setup(page, [{ pattern: /\/club\/coaches\/21\/slots\?/, respond: () => ok([{ ...slot, startsAtUtc: "2026-11-02T03:00:00", endsAtUtc: "2026-11-02T04:00:00" }]) }]);
    await page.goto("/coaches"); await page.getByLabel("Huấn luyện viên", { exact: true }).selectOption("21");
    await expect(page.getByRole("heading", { name: /10:00.*11:00/ })).toBeVisible();
  });
});
test("a refreshed score history cannot silently upgrade the version of an existing draft", async ({
  page,
}) => {
  let reads = 0;
  let payload: unknown;
  await setup(
    page,
    [
      {
        pattern: /\/club\/tournaments\/41$/,
        respond: () =>
          ok({
            tournament: { ...tournament, status: "Completed" },
            teams: [
              {
                id: 1,
                name: "A",
                players: "Alice",
                pool: 1,
                played: 1,
                won: 1,
                pointsFor: 21,
                pointsAgainst: 15,
              },
              {
                id: 2,
                name: "B",
                players: "Bob",
                pool: 1,
                played: 1,
                won: 0,
                pointsFor: 15,
                pointsAgainst: 21,
              },
            ],
            matches: [
              {
                id: 71,
                teamAId: 1,
                teamBId: 2,
                pool: 1,
                round: 1,
                startsAtUtc: slot.startsAtUtc,
                endsAtUtc: slot.endsAtUtc,
                location: "Sân A",
                scoreA: 21,
                scoreB: 15,
              },
            ],
          }),
      },
      {
        pattern: /\/club\/tournaments\/41\/matches\/71\/history$/,
        respond: () => {
          reads++;
          const first = {
            revision: 1,
            actorId: 1,
            oldScoreA: null,
            oldScoreB: null,
            scoreA: 21,
            scoreB: 15,
            reason: "Lần đầu",
            at: slot.endsAtUtc,
          };
          return ok(
            reads === 1
              ? [first]
              : [
                  first,
                  {
                    revision: 2,
                    actorId: 2,
                    oldScoreA: 21,
                    oldScoreB: 15,
                    scoreA: 15,
                    scoreB: 21,
                    reason: "Người khác vừa sửa",
                    at: slot.endsAtUtc,
                  },
                ],
          );
        },
      },
      {
        pattern: /\/club\/tournaments\/41\/matches\/71\/score$/,
        method: "PUT",
        respond: (_r, _u, body) => {
          payload = body;
          return ok("saved");
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/tournaments");
  await page.getByLabel("Giải đấu", { exact: true }).selectOption("41");
  await page.getByText("Xếp lịch / ghi kết quả trận #71").click();
  await expect(
    page.getByRole("button", { name: "Lưu kết quả trận 71", exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Điểm đội A").fill("19");
  await page.getByLabel("Lý do ghi / sửa kết quả").fill("Bản đang nhập");
  await page.getByRole("button", { name: "Kiểm tra lịch sử mới nhất" }).click();
  await expect(page.getByText(/Có phiên bản kết quả mới/)).toBeVisible();
  await expect(page.getByLabel("Điểm đội A")).toHaveValue("19");
  await expect(
    page.getByRole("button", { name: "Lưu kết quả trận 71", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Nạp kết quả mới nhất" }).click();
  await expect(page.getByLabel("Điểm đội A")).toHaveValue("15");
  await expect(page.getByLabel("Điểm đội B")).toHaveValue("21");
  await page
    .getByLabel("Lý do ghi / sửa kết quả")
    .fill("Đối chiếu sau khi nạp");
  await page.getByLabel("Điểm đội A").fill("21");
  await page.getByLabel("Điểm đội B").fill("17");
  await page
    .getByRole("button", { name: "Lưu kết quả trận 71", exact: true })
    .click();
  await expect
    .poll(() => payload)
    .toMatchObject({ scoreA: 21, scoreB: 17, expectedRevision: 2 });
});
async function setup(page: Page, handlers: Handler[], staff = false) {
  await seedLoggedInCustomer(page, {
    id: 1,
    fullName: staff ? "Nhân viên" : "Alice",
    role: staff ? "Manager" : "Customer",
    phone: "0900000001",
  });
  await mockApi(page, [
    ...handlers,
    { pattern: /\/branches(?:\?|$)/, respond: () => ok([branch]) },
    { pattern: /\/notifications\/unread-count/, respond: () => ok(0) },
    { pattern: /\/club\/equipment\?/, respond: () => ok([item]) },
    { pattern: /\/club\/coaches\?/, respond: () => ok([coach]) },
    { pattern: /\/club\/coaches\/21\/slots\?/, respond: () => ok([slot]) },
    { pattern: /\/club\/tournaments\?/, respond: () => ok([tournament]) },
    {
      pattern: /\/club\/calendar$/,
      method: "GET",
      respond: () => ok({ active: false }),
    },
    { pattern: /./, respond: () => ok([]) },
  ]);
}
test("matching uses skill area and Vietnam availability; sends request then withdraws", async ({
  page,
}) => {
  let status: string | null = null;
  let posted: unknown;
  let filter: URL | undefined;
  await setup(page, [
    {
      pattern: /\/club\/players\?/,
      respond: (_r, u) => {
        filter = u;
        return ok([
          {
            id: 9,
            ownerId: 2,
            owner: "Bob",
            branchId: 1,
            area: "Thủ Đức",
            level: 3,
            startsAtUtc: "2026-11-02T03:00:00Z",
            endsAtUtc: "2026-11-02T05:00:00Z",
            capacity: 4,
            accepted: 1,
            note: "Đánh đôi vui vẻ",
            closed: false,
            myStatus: status,
          },
        ]);
      },
    },
    {
      pattern: /\/club\/players\/9\/join$/,
      method: "POST",
      respond: () => {
        status = "Requested";
        return ok("saved");
      },
    },
    {
      pattern: /\/club\/players\/9\/leave$/,
      method: "POST",
      respond: () => {
        status = "Left";
        return ok("saved");
      },
    },
    {
      pattern: /\/club\/players$/,
      method: "POST",
      respond: (_r, _u, body) => {
        posted = body;
        return ok(10);
      },
    },
  ]);
  await page.goto("/players");
  await page.getByLabel("Trình độ", { exact: true }).selectOption("3");
  await page.getByLabel("Khu vực", { exact: true }).fill("Thủ");
  await page.getByLabel("Rảnh từ").fill("2026-11-02T09:00");
  await page.getByLabel("Rảnh đến").fill("2026-11-02T13:00");
  await page.getByRole("button", { name: "Tìm bạn", exact: true }).click();
  await expect
    .poll(() => filter?.searchParams.get("fromUtc"))
    .toBe("2026-11-02T02:00:00.000Z");
  expect(filter?.searchParams.get("area")).toBe("Thủ");
  expect(filter?.searchParams.get("level")).toBe("3");
  await page.getByRole("button", { name: "Xin tham gia", exact: true }).click();
  await expect(page.getByText("Chờ duyệt", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Rút tham gia", exact: true }).click();
  await expect(page.getByText("Đã rút", { exact: true })).toBeVisible();
  await page.getByLabel("Khu vực gặp mặt").fill("Bình Thạnh");
  await page.getByLabel("Bắt đầu", { exact: true }).fill("2026-11-03T18:00");
  await page.getByLabel("Kết thúc", { exact: true }).fill("2026-11-03T20:00");
  await page.getByLabel("Lời nhắn").fill("Tìm bạn mới");
  await page.getByRole("button", { name: "Đăng buổi chơi" }).click();
  await expect
    .poll(() => posted)
    .toMatchObject({
      branchId: 1,
      startsAtUtc: "2026-11-03T11:00:00.000Z",
      capacity: 4,
      level: 2,
    });
});
test("group expense selects booking and members then records contributions", async ({
  page,
}) => {
  let split: unknown;
  let paid = false;
  await setup(page, [
    {
      pattern: /\/groups\/joined$/,
      respond: () => ok([{ id: 7, name: "Nhóm A", ownerId: 1 }]),
    },
    {
      pattern: /\/groups\/7\/members$/,
      respond: () =>
        ok([
          { userId: 1, fullName: "Alice" },
          { userId: 2, fullName: "Bob" },
        ]),
    },
    {
      pattern: /\/club\/expenses\/bookings\?/,
      respond: () =>
        ok([
          {
            id: 91,
            code: "BB91",
            date: "2026-11-02",
            status: "Confirmed",
            payableAmount: 100001,
          },
        ]),
    },
    {
      pattern: /\/club\/expenses\/91$/,
      method: "GET",
      respond: () =>
        ok(
          split
            ? {
                id: 1,
                bookingId: 91,
                total: 100001,
                bookingCancelled: false,
                ownerId: 1,
                shares: [
                  { userId: 1, name: "Alice", amount: 50001, paidAt: null },
                  {
                    userId: 2,
                    name: "Bob",
                    amount: 50000,
                    paidAt: paid ? "2026-11-01T00:00:00Z" : null,
                  },
                ],
              }
            : null,
        ),
    },
    {
      pattern: /\/club\/expenses\/91$/,
      method: "POST",
      respond: (_r, _u, body) => {
        split = body;
        return ok("saved");
      },
    },
    {
      pattern: /\/club\/expenses\/91\/members\/2$/,
      method: "PUT",
      respond: (_r, _u, body) => {
        paid = body === true;
        return ok("saved");
      },
    },
  ]);
  await page.goto("/group-expenses");
  await page.getByLabel("Nhóm", { exact: true }).selectOption("7");
  await page.getByLabel("Đơn đặt sân của nhóm").selectOption("91");
  await page.getByLabel("Alice", { exact: true }).check();
  await page.getByLabel("Bob", { exact: true }).check();
  await page.getByRole("button", { name: "Chốt chia đều" }).click();
  await expect.poll(() => split).toEqual([1, 2]);
  await page.getByRole("button", { name: "Xác nhận đã đóng" }).nth(1).click();
  await expect(page.getByText(/Bob ·.*Đã đóng/)).toBeVisible();
  expect(paid).toBe(true);
});
test("equipment retry retains request key and uses Vietnam return date", async ({
  page,
}) => {
  const payloads: Record<string, unknown>[] = [];
  await setup(page, [
    {
      pattern: /\/club\/equipment\/orders$/,
      method: "POST",
      respond: (_r, _u, body) => {
        payloads.push(body as Record<string, unknown>);
        return payloads.length === 1
          ? err(503, "RETRY", "Tạm gián đoạn")
          : ok(1);
      },
    },
  ]);
  await page.goto("/equipment");
  await page
    .getByLabel("Hạn trả dự kiến (giờ Việt Nam)")
    .fill("2026-11-02T18:00");
  await page.getByRole("button", { name: "Đặt thuê", exact: true }).click();
  await expect(page.getByText("Tạm gián đoạn")).toBeVisible();
  await page.getByRole("button", { name: "Đặt thuê", exact: true }).click();
  await expect.poll(() => payloads.length).toBe(2);
  expect(payloads[0].requestKey).toBe(payloads[1].requestKey);
  expect(payloads[0]).toMatchObject({
    itemId: 11,
    quantity: 1,
    dueAtUtc: "2026-11-02T11:00:00.000Z",
  });
});
test("staff receives and returns rental with scoped API actions", async ({
  page,
}) => {
  let status = "Reserved";
  await setup(
    page,
    [
      {
        pattern: /\/club\/equipment\/orders\?/,
        respond: () =>
          ok([
            {
              id: 51,
              userId: 2,
              branchId: 1,
              item: "Vợt A",
              rental: true,
              quantity: 1,
              total: 20000,
              status,
              dueAtUtc: "2026-11-02T11:00:00Z",
              returnedAtUtc: null,
            },
          ]),
      },
      {
        pattern: /\/club\/equipment\/orders\/51\/issue$/,
        method: "POST",
        respond: () => {
          status = "Issued";
          return ok("saved");
        },
      },
      {
        pattern: /\/club\/equipment\/orders\/51\/return$/,
        method: "POST",
        respond: () => {
          status = "Returned";
          return ok("saved");
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/equipment");
  await page.getByRole("button", { name: "Xác nhận giao #51" }).click();
  await page.getByRole("button", { name: "Nhận trả #51" }).click();
  await expect(page.getByText(/Đã trả ·/)).toBeVisible();
});
test("coaching personal or group reservation submits people and server fee", async ({
  page,
}) => {
  let payload: unknown;
  await setup(page, [
    {
      pattern: /\/club\/coaches\/reservations$/,
      method: "POST",
      respond: (_r, _u, body) => {
        payload = body;
        return ok(61);
      },
    },
  ]);
  await page.goto("/coaches");
  await page.getByLabel("Huấn luyện viên", { exact: true }).selectOption("21");
  await page.getByLabel("Số học viên trong nhóm của bạn").fill("3");
  await page.getByRole("button", { name: "Đặt buổi học", exact: true }).click();
  await expect.poll(() => payload).toMatchObject({ slotId: 31, people: 3 });
  expect(payload).not.toHaveProperty("total");
});
test("tournament captain registers and staff draws then schedules a match", async ({
  page,
}) => {
  let registered: unknown;
  let drawn = false;
  let schedule: unknown;
  await setup(
    page,
    [
      {
        pattern: /\/club\/tournaments\/41$/,
        respond: () =>
          ok({
            tournament: {
              ...tournament,
              status: drawn ? "Scheduled" : "Registration",
            },
            teams: [
              {
                id: 1,
                captainId: 1,
                name: "Đội A",
                players: "Alice",
                pool: drawn ? 1 : 0,
                played: 0,
                won: 0,
                pointsFor: 0,
                pointsAgainst: 0,
              },
              {
                id: 2,
                captainId: 2,
                name: "Đội B",
                players: "Bob",
                pool: drawn ? 1 : 0,
                played: 0,
                won: 0,
                pointsFor: 0,
                pointsAgainst: 0,
              },
            ],
            matches: drawn
              ? [
                  {
                    id: 71,
                    teamAId: 1,
                    teamBId: 2,
                    pool: 1,
                    round: 1,
                    startsAtUtc: null,
                    endsAtUtc: null,
                    location: "",
                    scoreA: null,
                    scoreB: null,
                  },
                ]
              : [],
          }),
      },
      {
        pattern: /\/club\/tournaments\/41\/teams$/,
        method: "POST",
        respond: (_r, _u, body) => {
          registered = body;
          return ok("saved");
        },
      },
      {
        pattern: /\/club\/tournaments\/41\/draw$/,
        method: "POST",
        respond: () => {
          drawn = true;
          return ok("saved");
        },
      },
      {
        pattern: /\/club\/tournaments\/41\/matches\/71\/schedule$/,
        method: "PUT",
        respond: (_r, _u, body) => {
          schedule = body;
          return ok("saved");
        },
      },
    ],
    true,
  );
  await page.goto("/admin/club/tournaments");
  await page.getByLabel("Giải đấu", { exact: true }).selectOption("41");
  await page.getByLabel("Tên đội", { exact: true }).fill("Đội A");
  await page
    .getByLabel("Tên hiển thị các thành viên (không nhập SĐT)")
    .fill("Alice");
  await page.getByRole("button", { name: "Đăng ký đội", exact: true }).click();
  await expect
    .poll(() => registered)
    .toEqual({ name: "Đội A", players: "Alice" });
  await page.getByRole("button", { name: "Chia bảng vòng tròn" }).click();
  await page.getByText("Xếp lịch / ghi kết quả trận #71").click();
  await page.getByLabel("Bắt đầu trận").fill("2026-11-02T08:00");
  await page.getByLabel("Kết thúc trận").fill("2026-11-02T09:00");
  await page.getByLabel("Tên sân thi đấu").fill("Sân A");
  await page
    .getByRole("button", { name: "Lưu lịch trận 71", exact: true })
    .click();
  await expect
    .poll(() => schedule)
    .toEqual({
      startsAtUtc: "2026-11-02T01:00:00.000Z",
      endsAtUtc: "2026-11-02T02:00:00.000Z",
      location: "Sân A",
    });
});
test("calendar creates a subscription link and revokes it without persisting token", async ({
  page,
}) => {
  let active = false;
  await setup(page, [
    {
      pattern: /\/club\/calendar$/,
      method: "GET",
      respond: () => ok({ active }),
    },
    {
      pattern: /\/club\/calendar$/,
      method: "POST",
      respond: () => {
        active = true;
        return ok({ token: "a".repeat(64) });
      },
    },
    {
      pattern: /\/club\/calendar$/,
      method: "DELETE",
      respond: () => {
        active = false;
        return ok("saved");
      },
    },
  ]);
  await page.goto("/calendar-sync");
  await page.getByRole("button", { name: "Bật lịch tự đồng bộ" }).click();
  await expect(page.getByLabel("Liên kết lịch", { exact: true })).toHaveValue(
    new RegExp(`/api/v1/club/calendar/feed/${"a".repeat(64)}.ics$`),
  );
  await expect(
    page.getByRole("link", { name: "Mở đăng ký trong ứng dụng lịch" }),
  ).toHaveAttribute("href", /^webcal:/);
  await page.context().grantPermissions(["clipboard-write", "clipboard-read"]);
  await page.getByRole("button", { name: "Sao chép liên kết", exact: true }).click();
  await expect(page.getByText("Đã sao chép liên kết lịch.")).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toContain(`/api/v1/club/calendar/feed/${"a".repeat(64)}.ics`);
  expect(
    await page.evaluate(() =>
      JSON.stringify({ ...localStorage, ...sessionStorage }),
    ),
  ).not.toContain("a".repeat(64));
  await page.getByRole("button", { name: "Thu hồi liên kết" }).click();
  await expect(page.getByLabel("Liên kết lịch", { exact: true })).toHaveCount(
    0,
  );
  await expect(page.getByText("Chưa bật đồng bộ lịch.")).toBeVisible();
});
test("all club pages fit a mobile viewport with loading or empty data", async ({
  page,
}) => {
  await setup(page, []);
  await page.setViewportSize({ width: 375, height: 812 });
  for (const route of [
    "players",
    "group-expenses",
    "equipment",
    "coaches",
    "tournaments",
    "calendar-sync",
  ]) {
    await page.goto(`/${route}`);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth + 1,
      ),
      route,
    ).toBe(true);
    await expect(page.getByRole("navigation", { name: "Câu lạc bộ", exact: true }).locator('[aria-current="page"]')).toHaveCount(1);
    await page.screenshot({ path: `../artifacts/club-ui-20261029/${route}-mobile.png`, fullPage: true });
  }
  await page.screenshot({
    path: "../artifacts/club-features-20261027/calendar-mobile.png",
    fullPage: true,
  });
});

test("club navigation shows the current feature and links between all six desktop pages", async ({ page }) => {
  await setup(page, []);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/players");
  const navigation = page.getByRole("navigation", { name: "Câu lạc bộ", exact: true });
  for (const [label, path] of [
    ["Tìm bạn chơi", "/players"], ["Chia chi phí", "/group-expenses"],
    ["Dụng cụ", "/equipment"], ["Huấn luyện viên", "/coaches"],
    ["Giải đấu nhỏ", "/tournaments"], ["Lịch tự đồng bộ", "/calendar-sync"],
  ]) {
    await navigation.getByRole("link", { name: label, exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`${path}$`));
    await expect(navigation.getByRole("link", { name: label, exact: true })).toHaveAttribute("aria-current", "page");
    await expect(page.getByRole("list", { name: "Các bước thực hiện" })).toBeVisible();
    await page.screenshot({ path: `../artifacts/club-ui-20261029/${path.slice(1)}-desktop.png`, fullPage: true });
  }
});
