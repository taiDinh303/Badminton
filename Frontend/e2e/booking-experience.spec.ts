import { test, expect } from "./fixtures";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";
import type { Page } from "@playwright/test";

const court = { id: 1, branchId: 1, code: "C1", name: "Sân A", capacity: 4, isActive: true, priceLowPeak: 100000, pricePeak: 150000, weekendSurcharge: 0, ratingAvg: 5, ratingCount: 1 };
const slots = [{ id: 1, label: "10:00–11:00", startTime: "10:00:00", endTime: "11:00:00", isActive: true }, { id: 2, label: "11:00–12:00", startTime: "11:00:00", endTime: "12:00:00", isActive: true }];
const branch = { id: 1, name: "Cơ sở A", address: "Địa chỉ thử nghiệm", isActive: true, openTime: "06:00", closeTime: "23:00" };
const booking = { id: 91, bookingCode: "BB-91", courtId: 1, courtName: "Sân A", date: "2026-11-01", slotLabel: "10:00–11:00", status: "Confirmed", userId: 1, userName: "Khách", playerCount: 1, totalAmount: 100000, payableAmount: 100000, discountAmount: 0, pointsEarned: 0, pointsRedeemed: 0, createdAt: "2026-10-26T01:00:00Z", price: { basePrice: 100000, weekendSurcharge: 0, voucherDiscount: 0, pointsDiscount: 0, total: 100000, payable: 100000 } };
type Handler = Parameters<typeof mockApi>[1][number];
for (const status of ["Pending", "Confirmed"] as const) {
  test(`current group owner can cancel an original booker's ${status} booking`, async ({ page }) => {
    let cancelled = false;
    await setup(page, [
      { pattern: /\/bookings\/91$/, respond: () => ok({ ...booking, userId: 2, groupId: 7, status: cancelled ? "Cancelled" : status }) },
      { pattern: /\/groups\/mine$/, respond: () => ok([{ id: 7, name: "Nhóm đã chuyển chủ", ownerId: 1 }]) },
      { pattern: /\/bookings\/91\/cancel$/, method: "POST", respond: () => { cancelled = true; return ok({ ...booking, status: "Cancelled" }); } },
    ]);
    await page.goto("/bookings/91");
    await expect(page.getByRole("button", { name: "Huỷ đặt chỗ" })).toBeVisible();
    await expect(page.getByText("Thanh toán", { exact: true })).toHaveCount(0);
    await page.getByRole("button", { name: "Huỷ đặt chỗ" }).click();
    await expect.poll(() => cancelled).toBe(true);
    await expect(page.getByRole("button", { name: "Huỷ đặt chỗ" })).toHaveCount(0);
  });
}

test("an ordinary group member does not get the cancellation action", async ({ page }) => {
  await setup(page, [
    { pattern: /\/bookings\/91$/, respond: () => ok({ ...booking, userId: 2, groupId: 7 }) },
    { pattern: /\/groups\/mine$/, respond: () => ok([]) },
  ]);
  await page.goto("/bookings/91");
  await expect(page.getByText("Sân A", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Huỷ đặt chỗ" })).toHaveCount(0);
});

async function setup(page: Page, handlers: Handler[], role = "Customer") {
  await seedLoggedInCustomer(page, { id: 1, fullName: role === "Customer" ? "Khách" : "Nhân viên", role, phone: "0900000001" });
  await mockApi(page, [...handlers,
    { pattern: /\/courts(?:\?|$)/, respond: () => ok([court]) },
    { pattern: /\/time-slots(?:\?|$)/, respond: () => ok(slots) },
    { pattern: /\/branches(?:\?|$)/, respond: () => ok([branch]) },
    { pattern: /\/notifications\/unread-count/, respond: () => ok(0) },
    { pattern: /./, respond: () => ok([]) },
  ]);
}

test("planner quotes before an atomic batch and retains its key after retry", async ({ page }) => {
  const requests: Record<string, unknown>[] = []; let fail = true;
  await setup(page, [
    { pattern: /\/bookings\/quote$/, respond: () => ok(booking.price) },
    { pattern: /\/booking-experience\/batch$/, method: "POST", respond: (_r, _u, body) => { requests.push(body as Record<string, unknown>); if (fail) { fail = false; return err(409, "SLOT_CONFLICT", "Một slot vừa được đặt; chưa tạo đơn nào."); } return ok([booking]); } },
  ]);
  await page.goto("/planner");
  await page.getByLabel("Sân", { exact: true }).selectOption("1");
  await page.getByLabel("Ngày đầu tiên").fill("2026-11-01");
  await page.getByLabel("10:00–11:00", { exact: true }).check();
  await page.getByLabel("11:00–12:00", { exact: true }).check();
  await page.getByLabel("Số tuần (1 = không lặp)").fill("2");
  await page.getByRole("button", { name: "Xem tổng giá trước khi đặt" }).click();
  await expect(page.getByText("Tổng 4 đơn: 400.000đ")).toBeVisible();
  expect(requests).toHaveLength(0);
  await page.getByRole("button", { name: "Tạo toàn bộ lịch đã chọn" }).click();
  await expect(page.getByText("Một slot vừa được đặt; chưa tạo đơn nào.")).toBeVisible();
  await page.getByRole("button", { name: "Tạo toàn bộ lịch đã chọn" }).click();
  await expect(page.getByRole("link", { name: /BB-91/ })).toBeVisible();
  expect(requests).toHaveLength(2); expect(requests[0].requestKey).toBe(requests[1].requestKey);
  expect(requests[0]).toMatchObject({ expectedTotal: 400000, weeks: 2, slotIds: [1, 2] });
});

test("inbox read state and reminder preference use authenticated endpoints", async ({ page }) => {
  let read = false; let preferences: unknown;
  await setup(page, [
    { pattern: /\/notifications\/preferences$/, method: "PUT", respond: (_r, _u, body) => { preferences = body; return ok("saved"); } },
    { pattern: /\/notifications\/preferences$/, method: "GET", respond: () => ok({ remindersEnabled: true, minutesBefore: 60, email: false, sms: false, zalo: false }) },
    { pattern: /\/notifications\/3\/read$/, method: "PUT", respond: () => { read = true; return ok("read"); } },
    { pattern: /\/notifications\?/, respond: () => ok([{ id: 3, bookingId: 91, title: "Nhắc lịch sân A", body: "Sắp đến giờ chơi", createdAt: "2026-10-26T01:00:00Z", readAt: read ? "2026-10-26T01:01:00Z" : null }]) },
  ]);
  await page.goto("/notifications");
  await page.getByRole("button", { name: "Đánh dấu đã đọc" }).click();
  await expect(page.getByRole("button", { name: "Đánh dấu đã đọc" })).toHaveCount(0);
  await page.getByLabel("Nhắc trước giờ chơi").uncheck(); await page.getByLabel("Số phút trước giờ chơi").fill("120");
  await page.getByRole("button", { name: "Lưu tùy chọn" }).click();
  await expect.poll(() => preferences).toMatchObject({ remindersEnabled: false, minutesBefore: 120 });
});

for (const date of ["31/10/2026", "2026-10-31"]) {
  test(`booking tools preserve the original date ${date} when only the slot changes`, async ({ page }) => {
    let move: unknown;
    await setup(page, [
      { pattern: /\/bookings\/91$/, respond: () => ok({ ...booking, date }) },
      { pattern: /\/booking-experience\/91\/history$/, respond: () => ok([{ event: "created", detail: "Đã tạo đơn kiểm thử", at: booking.createdAt }]) },
      { pattern: /\/booking-experience\/91\/reschedule$/, method: "POST", respond: (_r, _u, body) => { move = body; return ok("moved"); } },
    ]);
    await page.goto("/bookings/91"); await page.getByRole("button", { name: "Đổi lịch, xem lịch sử, gửi phản ánh" }).click();
    await expect(page.getByText("Đã tạo đơn kiểm thử", { exact: false })).toBeVisible();
    await expect(page.getByLabel("Ngày đổi sang")).toHaveValue("2026-10-31");
    await page.getByLabel("Khung giờ mới").selectOption("2"); await page.getByLabel("Lý do đổi lịch").fill("Khách cần đổi giờ chơi cùng giá");
    await expect(page.getByRole("button", { name: "Xác nhận đổi lịch" })).toBeDisabled();
    await page.getByLabel("Tôi xác nhận đổi lịch.", { exact: false }).check();
    await page.getByRole("button", { name: "Xác nhận đổi lịch" }).click();
    await expect.poll(() => move).toMatchObject({ courtId: 1, slotId: 2, date: "2026-10-31", reason: "Khách cần đổi giờ chơi cùng giá" });
  });
}

test("staff calendar includes empty courts and switches week without losing its branch", async ({ page }) => {
  const requests: URL[] = [];
  await setup(page, [
    { pattern: /\/booking-experience\/calendar-courts/, respond: () => ok([{ courtId: 1, courtName: "Sân A", branchId: 1 }, { courtId: 2, courtName: "Sân trống B", branchId: 1 }]) },
    { pattern: /\/booking-experience\/calendar\?/, respond: (_r, url) => { requests.push(url); return ok([{ id: 91, code: "BB-91", courtId: 1, court: "Sân A", date: url.searchParams.get("from"), start: "10:00", end: "11:00", status: "Confirmed", checkedIn: false, customer: "Khách thử" }]); } },
  ], "Manager");
  await page.goto("/admin/operations/calendar"); await page.getByLabel("Cơ sở", { exact: true }).selectOption("1");
  await page.getByRole("button", { name: "Tuần", exact: true }).click();
  await expect(page.getByRole("columnheader")).toHaveCount(8);
  await expect(page.getByRole("rowheader", { name: "Sân trống B" })).toBeVisible();
  await expect.poll(() => requests.some(u => u.searchParams.get("branchId") === "1" && u.searchParams.get("from") !== u.searchParams.get("to"))).toBe(true);
});

test("case resolution requires verified attendance before accepting no-show appeal", async ({ page }) => {
  let resolution: unknown;
  await setup(page, [
    { pattern: /\/booking-experience\/cases\?/, respond: () => ok([{ id: 7, bookingId: 91, kind: "NoShow", reason: "Khách phản ánh đã đến sân", status: "Open", resolution: null, createdAt: booking.createdAt }]) },
    { pattern: /\/booking-experience\/cases\/7\/resolve$/, method: "POST", respond: (_r, _u, body) => { resolution = body; return ok("resolved"); } },
  ], "Manager");
  await page.goto("/admin/operations/cases"); await page.getByLabel("Kết quả xác minh #7").fill("Đã kiểm tra và xác minh khách có chơi");
  await expect(page.getByRole("button", { name: "Chấp nhận sau xác minh" })).toBeDisabled();
  await page.getByLabel("Đã xác minh khách thực sự đến chơi.", { exact: false }).check();
  await page.getByRole("button", { name: "Chấp nhận sau xác minh" }).click();
  await expect.poll(() => resolution).toMatchObject({ accepted: true });
});

test("incident announcement targets a selected scope and never cancels bookings automatically", async ({ page }) => {
  let sent: unknown; const writes: string[] = [];
  await setup(page, [
    { pattern: /\/operations\/incidents$/, method: "POST", respond: (_r, _u, body) => { sent = body; writes.push("announcement"); return ok(1); } },
    { pattern: /\/bookings\/.*\/cancel/, method: "POST", respond: () => { writes.push("cancel"); return ok("unexpected"); } },
  ], "Manager");
  await page.goto("/admin/operations/incidents"); await page.getByLabel("Cơ sở", { exact: true }).selectOption("1"); await page.getByLabel("Phạm vi sân").selectOption("1");
  await page.getByLabel("Nội dung thông báo cho khách").fill("Sân A có sự cố, nhân viên sẽ liên hệ phương án đổi");
  await page.getByLabel("Tôi đã kiểm tra phạm vi", { exact: false }).check();
  await page.getByRole("button", { name: "Gửi thông báo và lập danh sách liên hệ" }).click();
  await expect.poll(() => sent).toMatchObject({ branchId: 1, courtId: 1, slotId: null }); expect(writes).toEqual(["announcement"]);
});

test("shift entry converts explicit Vietnam time and handover has a result workflow", async ({ page }) => {
  let shift: unknown; let closed: unknown;
  await setup(page, [
    { pattern: /\/operations\/shift-staff/, respond: () => ok([{ id: 4, name: "Nhân viên A", branchId: 1 }]) },
    { pattern: /\/operations\/shifts$/, method: "POST", respond: (_r, _u, body) => { shift = body; return ok(1); } },
    { pattern: /\/operations\/handover\?/, respond: () => ok([{ id: 8, branchId: 1, text: "Kiểm tra đèn sân", createdBy: 4, createdAt: booking.createdAt, closedAt: null, resolution: null }]) },
    { pattern: /\/operations\/handover\/8\/close$/, method: "PUT", respond: (_r, _u, body) => { closed = body; return ok("done"); } },
  ], "Manager");
  await page.goto("/admin/operations/staff-work"); await page.getByLabel("Cơ sở", { exact: true }).selectOption("1"); await page.getByLabel("Nhân viên trực").selectOption("4");
  await page.getByLabel("Bắt đầu ca (giờ Việt Nam)").fill("2026-11-01T08:00"); await page.getByLabel("Kết thúc ca (giờ Việt Nam)").fill("2026-11-01T16:00");
  await page.getByRole("button", { name: "Tạo ca trực" }).click(); await expect.poll(() => shift).toMatchObject({ startsAtUtc: "2026-11-01T01:00:00.000Z", endsAtUtc: "2026-11-01T09:00:00.000Z" });
  await page.getByLabel("Kết quả bàn giao #8").fill("Đã kiểm tra đèn và bàn giao hoàn tất"); await page.getByRole("button", { name: "Hoàn tất việc bàn giao" }).click();
  await expect.poll(() => closed).toMatchObject({ outcome: "Đã kiểm tra đèn và bàn giao hoàn tất" });
});

test("favorites and privacy requests expose customer actions without administration rights", async ({ page }) => {
  let favorite = false; let privacy: unknown;
  await setup(page, [
    { pattern: /\/community\/favorites\/1$/, method: "PUT", respond: (_r, _u, body) => { favorite = body as boolean; return ok("saved"); } },
    { pattern: /\/community\/favorites$/, method: "GET", respond: () => ok(favorite ? [{ courtId: 1, branchId: 1, courtName: "Sân A" }] : []) },
    { pattern: /\/community\/privacy$/, method: "POST", respond: (_r, _u, body) => { privacy = body; return ok(1); } },
  ]);
  await page.goto("/favorites"); await page.getByRole("button", { name: "Yêu thích", exact: true }).click(); await expect(page.getByRole("button", { name: "Bỏ yêu thích" })).toBeVisible();
  await page.goto("/privacy"); await page.getByLabel("Loại yêu cầu").selectOption("Deletion"); await page.getByLabel("Nội dung cần xử lý").fill("Tôi đề nghị xem xét xóa dữ liệu hồ sơ"); await page.getByRole("button", { name: "Gửi yêu cầu dữ liệu" }).click();
  await expect.poll(() => privacy).toMatchObject({ kind: "Deletion" });
  await expect(page.getByRole("button", { name: "Ghi nhận đã xử lý" })).toHaveCount(0);
});

test("group meetings invite members by name and record individual RSVP", async ({ page }) => {
  let invitation: unknown; let response: unknown;
  await setup(page, [
    { pattern: /\/groups\/joined$/, respond: () => ok([{ id: 1, name: "Nhóm thứ Bảy", ownerId: 1, memberCount: 2, maxMembers: 4 }]) },
    { pattern: /\/community\/groups\/1\/meetings$/, respond: () => ok([{ id: 91, code: "BB-91", date: "2026-11-01", slot: "10:00", ownerId: 1, playerCount: 2 }]) },
    { pattern: /\/groups\/1\/members$/, respond: () => ok([{ userId: 1, fullName: "Khách" }, { userId: 2, fullName: "Bạn chơi B" }]) },
    { pattern: /\/booking-experience\/91\/rsvps$/, respond: () => ok([{ userId: 1, name: "Khách", attending: null }]) },
    { pattern: /\/booking-experience\/91\/invitations$/, method: "POST", respond: (_r, _u, body) => { invitation = body; return ok("invited"); } },
    { pattern: /\/booking-experience\/91\/rsvp$/, method: "PUT", respond: (_r, _u, body) => { response = body; return ok("responded"); } },
  ]);
  await page.goto("/group-meetings"); await page.getByLabel("Nhóm của bạn").selectOption("1"); await page.getByLabel("Lịch chơi của nhóm").selectOption("91");
  await page.getByLabel("Bạn chơi B", { exact: true }).check(); await page.getByRole("button", { name: "Mời thành viên buổi chơi" }).click();
  await expect.poll(() => invitation).toEqual([2]); await page.getByRole("button", { name: "Tôi tham gia", exact: true }).click(); await expect.poll(() => response).toBe(true);
});

test("new customer pages fit a 320px phone", async ({ page }) => {
  test.setTimeout(60000); await page.setViewportSize({ width: 320, height: 640 });
  await setup(page, [{ pattern: /\/notifications\/preferences$/, respond: () => ok({ remindersEnabled: true, minutesBefore: 60, email: false, sms: false, zalo: false }) }]);
  for (const path of ["/planner", "/notifications", "/favorites", "/privacy", "/group-meetings", "/media-library"]) {
    await page.goto(path); await expect(page.locator("#main-content")).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
});

test("front desk looks up the customer and creates a pending order owned by that customer", async ({ page }) => {
  let posted: unknown;
  await setup(page, [
    { pattern: /\/booking-experience\/desk-customer\?/, respond: () => ok({ id: 2, name: "Khách B", isVerified: true }) },
    { pattern: /\/booking-experience\/calendar-courts/, respond: () => ok([{ courtId: 1, courtName: "Sân A", branchId: 1 }]) },
    { pattern: /\/booking-experience\/desk$/, method: "POST", respond: (_r, _u, body) => { posted = body; return ok({ ...booking, id: 99, bookingCode: "BB-DESK", userId: 2, status: "Pending" }); } },
  ], "Manager");
  await page.goto("/admin/operations/calendar");
  await page.getByLabel("Ngày bắt đầu lịch").fill("2026-11-01");
  await page.getByLabel("SĐT khách đã có tài khoản").fill("0900000002"); await page.getByRole("button", { name: "Tìm khách" }).click();
  await expect(page.getByText("Khách B", { exact: true })).toBeVisible();
  await page.getByLabel("Sân đặt hộ").selectOption("1"); await page.getByLabel("Khung giờ đặt hộ").selectOption("1");
  await page.getByRole("button", { name: "Tạo đơn đặt hộ" }).click();
  await expect.poll(() => posted).toMatchObject({ customerId: 2, date: "2026-11-01", courtId: 1, slotId: 1 });
  await expect(page.getByRole("link", { name: "Xem đơn BB-DESK" })).toBeVisible();
});

test("image upload uses multipart and private image preview keeps authentication", async ({ page }) => {
  let uploaded = false; let multipart = false; let authenticated = false;
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j2ioAAAAASUVORK5CYII=", "base64");
  await setup(page, [
    { pattern: /\/media(?:\?|$)/, method: "GET", respond: () => ok(uploaded ? [{ id: 12, url: "/api/v1/media/12", contentType: "image/png", length: png.length }] : []) },
    { pattern: /\/media$/, method: "POST", respond: (route) => { multipart = route.request().headers()["content-type"].startsWith("multipart/form-data;"); uploaded = true; return ok({ id: 12, url: "/api/v1/media/12", contentType: "image/png", length: png.length }); } },
  ]);
  await page.route(/\/api\/v1\/media\/12$/, async route => { authenticated = !!route.request().headers().authorization; await route.fulfill({ contentType: "image/png", body: png }); });
  await page.goto("/media-library"); await page.locator('input[type="file"]').setInputFiles({ name: "photo.png", mimeType: "image/png", buffer: png });
  await page.getByRole("button", { name: "Tải ảnh lên" }).click();
  await expect(page.getByRole("img", { name: "Ảnh đã tải lên" })).toBeVisible();
  await expect.poll(() => page.getByRole("img", { name: "Ảnh đã tải lên" }).evaluate(element => (element as HTMLImageElement).naturalWidth)).toBe(1);
  expect(multipart).toBe(true); expect(authenticated).toBe(true);
});

test("official review replies and moderation preserve a visible reason and original content", async ({ page }) => {
  let reply: unknown; let moderation: unknown;
  await setup(page, [
    { pattern: /\/reviews\/paged\?/, respond: () => ok({ items: [{ id: 5, bookingId: 91, courtId: 1, courtName: "Sân A", userName: "Khách B", rating: 4, comment: "Nội dung đánh giá gốc", createdAt: booking.createdAt }], totalItems: 1, totalPages: 1, currentPage: 1, averageRating: 4 }) },
    { pattern: /\/community\/reviews\/5\/reply$/, method: "PUT", respond: (_r, _u, body) => { reply = body; return ok("saved"); } },
    { pattern: /\/community\/review-reports\?/, respond: () => ok([{ id: 6, reviewId: 5, reason: "Cần xác minh nội dung", status: "Open", resolution: null, comment: "Nội dung đánh giá gốc", rating: 4 }]) },
    { pattern: /\/community\/reviews\/5\/moderation$/, method: "PUT", respond: (_r, _u, body) => { moderation = body; return ok("saved"); } },
  ], "Manager");
  await page.goto("/reviews"); await page.getByRole("button", { name: "Ảnh, phản hồi và báo cáo đánh giá" }).click();
  await page.getByLabel("Phản hồi chính thức").fill("Cơ sở đã ghi nhận và sẽ cải thiện chất lượng sân"); await page.getByRole("button", { name: "Lưu phản hồi" }).click();
  await expect.poll(() => reply).toMatchObject({ text: "Cơ sở đã ghi nhận và sẽ cải thiện chất lượng sân" });
  await page.goto("/admin/operations/moderation"); await expect(page.getByText("Nội dung gốc (4/5 sao): Nội dung đánh giá gốc")).toBeVisible();
  await page.getByLabel("Lý do kiểm duyệt #6").fill("Đã kiểm tra nội dung và cần ẩn theo báo cáo");
  await page.getByRole("button", { name: "Ẩn đánh giá có lý do" }).click(); await expect.poll(() => moderation).toMatchObject({ hide: true });
});

test("delivery retry refreshes actual status and CSV export produces an Excel-compatible file", async ({ page }) => {
  let retried = false;
  await setup(page, [
    { pattern: /\/notifications\/deliveries\?/, respond: () => ok([{ id: 4, bookingId: 91, channel: "email", status: retried ? "Delivered" : "Failed", error: retried ? null : "Relay unavailable", attempts: retried ? 3 : 2, createdAt: booking.createdAt, lastAttempt: null }]) },
    { pattern: /\/notifications\/deliveries\/4\/retry$/, method: "POST", respond: () => { retried = true; return ok("attempted"); } },
    { pattern: /\/operations\/insights\?/, respond: () => ok({ customers: 3, newCustomers: 1, returningCustomers: 2, bookings: 4, noShows: 1, noShowRate: 25, waiting: 2, promoted: 1 }) },
    { pattern: /\/operations\/booking-export\?/, respond: () => ok("Code,Date\r\nBB-91,2026-11-01\r\n") },
  ], "Manager");
  await page.goto("/admin/operations/deliveries"); await page.getByRole("button", { name: "Thử gửi lại" }).click(); await expect(page.getByText("#4 · email · Delivered")).toBeVisible(); await expect(page.getByText("Số lần thử: 3")).toBeVisible();
  await page.goto("/admin/operations/insights"); const download = page.waitForEvent("download"); await page.getByRole("button", { name: "Xuất đơn CSV" }).click(); const file = await download;
  expect(file.suggestedFilename()).toMatch(/^bookings-.*\.csv$/);
  expect(await file.failure()).toBeNull();
});

test("a booked slot offers alternatives that update the selected court", async ({ page }) => {
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j2ioAAAAASUVORK5CYII=", "base64");
  await setup(page, [
    { pattern: /\/bookings\/availability\//, respond: () => ok([{ courtId: 1, courtName: "Sân A", priceLowPeak: 100000, pricePeak: 150000, capacity: 4, slots: [{ slotId: 1, label: "18:00", startTime: "18:00", endTime: "19:00", isPeak: false, isLocked: true, lockReason: "Đã đặt" }] }, { courtId: 2, courtName: "Sân B", priceLowPeak: 100000, pricePeak: 150000, capacity: 4, slots: [{ slotId: 1, label: "18:00", startTime: "18:00", endTime: "19:00", isPeak: false, isLocked: false, lockReason: "" }] }]) },
    { pattern: /\/bookings\/quote$/, respond: () => ok(booking.price) },
    { pattern: /\/booking-experience\/alternatives\?/, respond: () => ok([{ courtId: 2, court: "Sân B", slotId: 1, label: "18:00", date: "2026-11-01", price: 100000 }]) },
    { pattern: /\/media\?courtId=2/, respond: () => ok([{ id: 13, url: "/api/v1/media/13", contentType: "image/png", length: png.length }]) },
  ]);
  await page.route(/\/api\/v1\/media\/13$/, route => route.fulfill({ contentType: "image/png", body: png }));
  await page.goto("/book"); await page.getByRole("button", { name: "18:00 Đã đặt", exact: true }).click();
  await page.getByRole("button", { name: "Gợi ý sân / giờ thay thế" }).click(); await page.getByRole("button", { name: "Sân B · 18:00", exact: true }).click();
  await expect(page.getByRole("dialog", { name: "Xác nhận đặt sân" }).getByText("Sân B", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Xác nhận đặt sân" })).toBeEnabled();
  await page.getByRole("button", { name: "Xem ảnh sân", exact: true }).click();
  await expect(page.getByRole("img", { name: "Ảnh đã tải lên" })).toBeVisible();
  await expect.poll(() => page.getByRole("img", { name: "Ảnh đã tải lên" }).evaluate(element => (element as HTMLImageElement).naturalWidth)).toBe(1);
});
