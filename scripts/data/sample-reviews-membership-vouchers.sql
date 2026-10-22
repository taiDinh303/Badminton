-- Manual, user-requested sample data. NEVER run at application startup.
-- All new accounts/reviews/codes are visibly labeled; no payments or points.
-- Rerunning is idempotent. Existing memberships/bookings/vouchers are preserved.
BEGIN;
SELECT pg_advisory_xact_lock(-2, 20261022);
SET LOCAL search_path = public, extensions;
DO $$
DECLARE
  i integer; uid integer; bid integer; cid integer; branch_id integer; slot_id integer;
  names text[] := ARRAY['An','Bình','Chi','Dũng','Hà','Linh'];
  comments text[] := ARRAY['Mặt sân sạch, ánh sáng tốt.', 'Nhân viên hướng dẫn nhiệt tình.', 'Khung giờ rõ ràng, đặt sân thuận tiện.', 'Sân thoáng, phù hợp chơi nhóm.', 'Khu vực nghỉ ngơi gọn gàng.', 'Trải nghiệm tốt, sẽ quay lại.'];
  courts integer[];
BEGIN
  SELECT array_agg("Id" ORDER BY "Id") INTO courts FROM "Courts" WHERE "IsActive" AND "DeletedAt" IS NULL;
  SELECT "Id" INTO slot_id FROM "TimeSlots" WHERE "IsActive" AND "StartTime" >= TIME '06:00' ORDER BY "StartTime" LIMIT 1;
  IF array_length(courts, 1) IS NULL OR slot_id IS NULL THEN RAISE EXCEPTION 'Active court and daytime slot required'; END IF;
  FOR i IN 1..6 LOOP
    SELECT "Id" INTO uid FROM "Users" WHERE "PhoneNumber" = '00000001' || lpad(i::text, 2, '0');
    IF uid IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id"=uid AND "FullName"='[MẪU] '||names[i]) THEN
      RAISE EXCEPTION 'Sample phone collision; existing account preserved';
    END IF;
    IF uid IS NULL THEN
      INSERT INTO "Users" ("FullName","PhoneNumber","PasswordHash","Role","Status","IsVerified","NoShowCount","NoShowPeriod","NoShowLimited","Tier","MembershipExpiresAt","CreatedAt")
      VALUES ('[MẪU] '||names[i], '00000001'||lpad(i::text,2,'0'), extensions.crypt(gen_random_uuid()::text,extensions.gen_salt('bf')), 'Customer','Active',false,0,CURRENT_DATE,false,
        CASE WHEN i IN (2,4) THEN 'Gold' ELSE 'Silver' END,
        now() + CASE WHEN i=6 THEN INTERVAL '-2 days' WHEN i IN (2,4) THEN INTERVAL '90 days' ELSE INTERVAL '30 days' END,now()) RETURNING "Id" INTO uid;
    END IF;
    cid := courts[1 + ((i-1) % array_length(courts,1))];
    SELECT "BranchId" INTO branch_id FROM "Courts" WHERE "Id"=cid;
    SELECT "Id" INTO bid FROM "Bookings" WHERE "BookingCode"='DEMO-REVIEW-'||lpad(i::text,2,'0');
    IF bid IS NULL THEN
      INSERT INTO "Bookings" ("BookingCode","UserId","CourtId","SlotId","BranchId","Date","PlayerCount","Status","TotalAmount","DiscountAmount","PayableAmount","PointsEarned","PointsRedeemed","Note","CreatedAt")
      VALUES ('DEMO-REVIEW-'||lpad(i::text,2,'0'),uid,cid,slot_id,branch_id,CURRENT_DATE-45-i,2,'Completed',0,0,0,0,0,'[DỮ LIỆU MẪU] Chỉ minh họa đánh giá, không có thanh toán.',now()-INTERVAL '45 days') RETURNING "Id" INTO bid;
    ELSIF NOT EXISTS (SELECT 1 FROM "Bookings" WHERE "Id"=bid AND "UserId"=uid AND "Note" LIKE '[DỮ LIỆU MẪU]%') THEN
      RAISE EXCEPTION 'Sample booking collision; existing booking preserved';
    END IF;
    INSERT INTO "Reviews" ("BookingId","CourtId","UserId","Rating","Comment","FlaggedRefunded","CreatedAt")
    SELECT bid,cid,uid,CASE WHEN i IN (2,5) THEN 4 ELSE 5 END,'[DỮ LIỆU MẪU] '||comments[i],false,now()-make_interval(days=>i)
    WHERE NOT EXISTS (SELECT 1 FROM "Reviews" WHERE "BookingId"=bid);
  END LOOP;

  INSERT INTO "Vouchers" ("Code","Type","Value","MaxValue","MinSpend","MaxPerUser","TotalQty","UsedQty","ValidFrom","ValidUntil","IsActive","CreatedAt")
  SELECT v.code,v.typ,v.val,v.cap,v.min_spend,1,v.qty,0,CURRENT_DATE-30,CURRENT_DATE+v.days,v.active,now()
  FROM (VALUES
    ('DEMO10','Percent',10,50000,100000,100,30,true),
    ('DEMO20K','Fixed',20000,NULL::integer,100000,80,30,true),
    ('DEMO15','Percent',15,60000,200000,60,45,true),
    ('DEMO30K','Fixed',30000,NULL::integer,250000,50,60,true),
    ('DEMO-HETHAN','Percent',5,20000,100000,20,-1,true),
    ('DEMO-TAMDUNG','Fixed',10000,NULL::integer,100000,20,30,false)
  ) AS v(code,typ,val,cap,min_spend,qty,days,active)
  WHERE NOT EXISTS (SELECT 1 FROM "Vouchers" x WHERE x."Code"=v.code);

  -- Keep historical references; only deactivate the invalid 02:00 slot.
  UPDATE "TimeSlots" SET "IsActive"=false,"UpdatedAt"=now() WHERE "StartTime"=TIME '02:00' AND "IsActive";
  UPDATE "Courts" c SET "RatingAvg"=r.avg,"RatingCount"=r.n,"UpdatedAt"=now()
  FROM (SELECT "CourtId",round(avg("Rating")::numeric,2)::double precision AS avg,count(*)::integer AS n FROM "Reviews" GROUP BY "CourtId") r
  WHERE c."Id"=r."CourtId" AND c."Id"=ANY(courts);
END $$;
COMMIT;
