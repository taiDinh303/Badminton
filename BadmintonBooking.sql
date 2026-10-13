/* ============================================================================
   BadmintonBooking.sql — SQL Server LocalDB (dev)
   ------------------------------------------------------------------------
   Tạo lại CSDL BadmintonBooking sạch:
     * 15 bảng + index (đúng migration 20261013131702_InitialCreate)
     * Dữ liệu seed: 1 chi nhánh, 17 khung giờ 06:00–22:00, 6 sân, 4 voucher
     * 4 tài khoản (mật khẩu chung: Hovaten123@, đã hash BCrypt thật):
         0900000003  Customer
         0900000004  Customer
         0900000005  Manager (branch 1)
         0900000006  Admin  (duy nhất 1 admin Active — index ux_active_admin)
     * __EFMigrationsHistory (để `update-database` nhận là up-to-date)

   CÁCH DÙNG:
     1. Dừng app đang chạy (release DB).
     2. Chạy script này trên (localdb)\MSSQLLocalDB (SSMS/Data Explorer/SqlPackage).
        Script sẽ DROP + tạo lại DB nên DỮ LIỆU CŨ BỊ XÓA (phục vụ dev).
   ============================================================================ */

USE [master];
GO
IF DB_ID(N'BadmintonBooking') IS NOT NULL
BEGIN
    ALTER DATABASE [BadmintonBooking] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [BadmintonBooking];
END
GO
CREATE DATABASE [BadmintonBooking];
GO
USE [BadmintonBooking];
GO

/* ============================================================================
   1. TABLES (đúng DDL EF Core 8 — SqlServer)
   ============================================================================ */

IF OBJECT_ID(N'[Branches]') IS NULL
BEGIN
CREATE TABLE [Branches] (
    [Id]          int           NOT NULL IDENTITY(1,1),
    [Name]        nvarchar(100) NOT NULL,
    [Address]     nvarchar(255) NOT NULL,
    [Description] nvarchar(max) NULL,
    [GeoLat]      float         NULL,
    [GeoLng]      float         NULL,
    [OpenTime]    time          NOT NULL,
    [CloseTime]   time          NOT NULL,
    [Status]      nvarchar(20)  NOT NULL DEFAULT N'Active',
    [IsActive]    bit           NOT NULL,
    [CreatedAt]   datetime2     NOT NULL,
    [UpdatedAt]   datetime2     NULL,
    [DeletedAt]   datetime2     NULL,
    CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
);
END
GO

IF OBJECT_ID(N'[TimeSlots]') IS NULL
BEGIN
CREATE TABLE [TimeSlots] (
    [Id]        int           NOT NULL IDENTITY(1,1),
    [Label]     nvarchar(20)  NOT NULL,
    [StartTime] time          NOT NULL,
    [EndTime]   time          NOT NULL,
    [IsPeak]    bit           NOT NULL,
    [IsActive]  bit           NOT NULL,
    [CreatedAt] datetime2     NOT NULL,
    [UpdatedAt] datetime2     NULL,
    [DeletedAt] datetime2     NULL,
    CONSTRAINT [PK_TimeSlots] PRIMARY KEY ([Id])
);
END
GO

IF OBJECT_ID(N'[Vouchers]') IS NULL
BEGIN
CREATE TABLE [Vouchers] (
    [Id]         int           NOT NULL IDENTITY(1,1),
    [Code]       nvarchar(30)  NOT NULL,
    [Type]       nvarchar(10)  NOT NULL,
    [Value]      int           NOT NULL,
    [MaxValue]   int           NULL,
    [MinSpend]   int           NOT NULL,
    [MaxPerUser] int           NOT NULL,
    [TotalQty]   int           NOT NULL,
    [UsedQty]    int           NOT NULL,
    [ValidFrom]  date          NOT NULL,
    [ValidUntil] date          NULL,
    [IsActive]   bit           NOT NULL,
    [CreatedAt]  datetime2     NOT NULL,
    [UpdatedAt]  datetime2     NULL,
    [DeletedAt]  datetime2     NULL,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id])
);
END
GO

IF OBJECT_ID(N'[Courts]') IS NULL
BEGIN
CREATE TABLE [Courts] (
    [Id]               int           NOT NULL IDENTITY(1,1),
    [BranchId]         int           NOT NULL,
    [Code]             nvarchar(20)  NOT NULL,
    [Name]             nvarchar(100) NOT NULL,
    [Surface]          nvarchar(20)  NOT NULL DEFAULT N'Sindalo',
    [PriceLowPeak]     int           NOT NULL,
    [PricePeak]        int           NOT NULL,
    [WeekendSurcharge] decimal(10,4) NOT NULL,
    [Capacity]         int           NOT NULL,
    [Amenities]        nvarchar(500) NULL,
    [ImageUrl]         nvarchar(500) NULL,
    [RatingAvg]        float         NULL,
    [RatingCount]      int           NOT NULL,
    [IsActive]         bit           NOT NULL,
    [CreatedAt]        datetime2     NOT NULL,
    [UpdatedAt]        datetime2     NULL,
    [DeletedAt]        datetime2     NULL,
    CONSTRAINT [PK_Courts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Courts_Branches_BranchId] FOREIGN KEY ([BranchId])
        REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[Users]') IS NULL
BEGIN
CREATE TABLE [Users] (
    [Id]           int           NOT NULL IDENTITY(1,1),
    [FullName]     nvarchar(100) NOT NULL,
    [PhoneNumber]  nvarchar(15)  NOT NULL,
    [Email]        nvarchar(150) NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [AvatarUrl]    nvarchar(500) NULL,
    [Role]         nvarchar(20)  NOT NULL DEFAULT N'Customer',
    [BranchId]     int           NULL,
    [Status]       nvarchar(20)  NOT NULL DEFAULT N'Active',
    [IsVerified]   bit           NOT NULL,
    [CreatedAt]    datetime2     NOT NULL,
    [UpdatedAt]    datetime2     NULL,
    [DeletedAt]    datetime2     NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Branches_BranchId] FOREIGN KEY ([BranchId])
        REFERENCES [Branches] ([Id]) ON DELETE SET NULL
);
END
GO

IF OBJECT_ID(N'[CourtDateStatus]') IS NULL
BEGIN
CREATE TABLE [CourtDateStatus] (
    [CourtId]     int            NOT NULL,
    [Date]        date           NOT NULL,
    [Status]      nvarchar(20)   NOT NULL DEFAULT N'Open',
    [Note]        nvarchar(max)  NULL,
    [CreatedById] int            NULL,
    [CreatedAt]   datetime2      NOT NULL,
    [UpdatedAt]   datetime2      NULL,
    CONSTRAINT [PK_CourtDateStatus] PRIMARY KEY ([CourtId], [Date]),
    CONSTRAINT [FK_CourtDateStatus_Courts_CourtId] FOREIGN KEY ([CourtId])
        REFERENCES [Courts] ([Id]) ON DELETE CASCADE
);
END
GO

IF OBJECT_ID(N'[AuditLogs]') IS NULL
BEGIN
CREATE TABLE [AuditLogs] (
    [Id]         bigint         NOT NULL IDENTITY(1,1),
    [ActorId]    int            NULL,
    [Action]     nvarchar(60)   NOT NULL,
    [EntityType] nvarchar(40)   NOT NULL,
    [EntityId]   nvarchar(40)   NULL,
    [BeforeJson] nvarchar(2000) NULL,
    [AfterJson]  nvarchar(2000) NULL,
    [Reason]     nvarchar(500)  NULL,
    [CreatedAt]  datetime2      NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AuditLogs_Users_ActorId] FOREIGN KEY ([ActorId])
        REFERENCES [Users] ([Id]) ON DELETE SET NULL
);
END
GO

IF OBJECT_ID(N'[PlayGroups]') IS NULL
BEGIN
CREATE TABLE [PlayGroups] (
    [Id]         int          NOT NULL IDENTITY(1,1),
    [Name]       nvarchar(100) NOT NULL,
    [OwnerId]    int          NOT NULL,
    [MaxMembers] int          NOT NULL,
    [CreatedAt]  datetime2    NOT NULL,
    [UpdatedAt]  datetime2    NULL,
    [DeletedAt]  datetime2    NULL,
    CONSTRAINT [PK_PlayGroups] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlayGroups_Users_OwnerId] FOREIGN KEY ([OwnerId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[Bookings]') IS NULL
BEGIN
CREATE TABLE [Bookings] (
    [Id]                    int           NOT NULL IDENTITY(1,1),
    [BookingCode]           nvarchar(30)  NOT NULL,
    [IdempotencyKey]        nvarchar(80)  NULL,
    [UserId]                int           NOT NULL,
    [GroupId]               int           NULL,
    [CourtId]               int           NOT NULL,
    [SlotId]                int           NOT NULL,
    [BranchId]              int           NOT NULL,
    [Date]                  date          NOT NULL,
    [PlayerCount]           int           NOT NULL,
    [Status]                nvarchar(20)  NOT NULL DEFAULT N'Pending',
    [TotalAmount]           int           NOT NULL,
    [DiscountAmount]        int           NOT NULL,
    [PayableAmount]         int           NOT NULL,
    [PointsEarned]          int           NOT NULL,
    [PendingExpiresAt]      datetime2     NULL,
    [Note]                  nvarchar(max) NULL,
    [CancelReason]          nvarchar(max) NULL,
    [CancelledAt]           datetime2     NULL,
    [VoucherRedemptionId]   int           NULL,
    [CreatedAt]             datetime2     NOT NULL,
    [UpdatedAt]             datetime2     NULL,
    [DeletedAt]             datetime2     NULL,
    CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bookings_Branches_BranchId] FOREIGN KEY ([BranchId])
        REFERENCES [Branches] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Bookings_Courts_CourtId] FOREIGN KEY ([CourtId])
        REFERENCES [Courts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Bookings_PlayGroups_GroupId] FOREIGN KEY ([GroupId])
        REFERENCES [PlayGroups] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Bookings_TimeSlots_SlotId] FOREIGN KEY ([SlotId])
        REFERENCES [TimeSlots] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Bookings_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[GroupMembers]') IS NULL
BEGIN
CREATE TABLE [GroupMembers] (
    [GroupId]   int         NOT NULL,
    [UserId]    int         NOT NULL,
    [JoinedAt]  datetime2   NOT NULL,
    CONSTRAINT [PK_GroupMembers] PRIMARY KEY ([GroupId], [UserId]),
    CONSTRAINT [FK_GroupMembers_PlayGroups_GroupId] FOREIGN KEY ([GroupId])
        REFERENCES [PlayGroups] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GroupMembers_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[BookingPlayers]') IS NULL
BEGIN
CREATE TABLE [BookingPlayers] (
    [BookingId] int         NOT NULL,
    [UserId]    int         NOT NULL,
    [IsCreator] bit         NOT NULL,
    [CreatedAt] datetime2   NOT NULL,
    CONSTRAINT [PK_BookingPlayers] PRIMARY KEY ([BookingId], [UserId]),
    CONSTRAINT [FK_BookingPlayers_Bookings_BookingId] FOREIGN KEY ([BookingId])
        REFERENCES [Bookings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BookingPlayers_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[Payments]') IS NULL
BEGIN
CREATE TABLE [Payments] (
    [Id]           int           NOT NULL IDENTITY(1,1),
    [BookingId]    int           NOT NULL,
    [UserId]       int           NOT NULL,
    [Amount]       int           NOT NULL,
    [Method]       nvarchar(20)  NOT NULL,
    [GatewayRef]   nvarchar(120) NULL,
    [Status]       nvarchar(20)  NOT NULL DEFAULT N'Pending',
    [RefundAmount] int           NOT NULL,
    [RefundedAt]   datetime2     NULL,
    [RawResponse]  nvarchar(2000) NULL,
    [PaidAt]       datetime2     NULL,
    [CreatedAt]    datetime2     NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Bookings_BookingId] FOREIGN KEY ([BookingId])
        REFERENCES [Bookings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[PointLedger]') IS NULL
BEGIN
CREATE TABLE [PointLedger] (
    [Id]        int          NOT NULL IDENTITY(1,1),
    [UserId]    int          NOT NULL,
    [Delta]     int          NOT NULL,
    [Reason]    nvarchar(100) NOT NULL,
    [BookingId] int          NULL,
    [CreatedAt] datetime2    NOT NULL,
    CONSTRAINT [PK_PointLedger] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PointLedger_Bookings_BookingId] FOREIGN KEY ([BookingId])
        REFERENCES [Bookings] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_PointLedger_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[Reviews]') IS NULL
BEGIN
CREATE TABLE [Reviews] (
    [Id]             int            NOT NULL IDENTITY(1,1),
    [BookingId]      int            NOT NULL,
    [CourtId]        int            NOT NULL,
    [UserId]         int            NOT NULL,
    [Rating]         int            NOT NULL,
    [Comment]        nvarchar(1000) NULL,
    [Images]         nvarchar(500)  NULL,
    [FlaggedRefunded] bit          NOT NULL,
    [CreatedAt]      datetime2      NOT NULL,
    CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reviews_Bookings_BookingId] FOREIGN KEY ([BookingId])
        REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reviews_Courts_CourtId] FOREIGN KEY ([CourtId])
        REFERENCES [Courts] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[VoucherRedemptions]') IS NULL
BEGIN
CREATE TABLE [VoucherRedemptions] (
    [Id]             int         NOT NULL IDENTITY(1,1),
    [VoucherId]      int         NOT NULL,
    [UserId]         int         NOT NULL,
    [BookingId]      int         NULL,
    [DiscountAmount] int         NOT NULL,
    [RedeemedAt]     datetime2   NOT NULL,
    CONSTRAINT [PK_VoucherRedemptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VoucherRedemptions_Bookings_BookingId] FOREIGN KEY ([BookingId])
        REFERENCES [Bookings] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_VoucherRedemptions_Users_UserId] FOREIGN KEY ([UserId])
        REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_VoucherRedemptions_Vouchers_VoucherId] FOREIGN KEY ([VoucherId])
        REFERENCES [Vouchers] ([Id]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
CREATE TABLE [__EFMigrationsHistory] (
    [MigrationId]    nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32)  NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);
END
GO

/* ============================================================================
   2. INDEXES (gồm 2 index CHỐT: chống trùng chỗ + chỉ 1 admin Active)
   ============================================================================ */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_ActorId')
    CREATE INDEX [IX_AuditLogs_ActorId] ON [AuditLogs]([ActorId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_EntityType_EntityId_CreatedAt')
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId_CreatedAt] ON [AuditLogs]([EntityType],[EntityId],[CreatedAt]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingPlayers_UserId')
    CREATE INDEX [IX_BookingPlayers_UserId] ON [BookingPlayers]([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_BookingCode')
    CREATE UNIQUE INDEX [IX_Bookings_BookingCode] ON [Bookings]([BookingCode]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_BranchId_Date')
    CREATE INDEX [IX_Bookings_BranchId_Date] ON [Bookings]([BranchId],[Date]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_Date')
    CREATE INDEX [IX_Bookings_Date] ON [Bookings]([Date]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_GroupId')
    CREATE INDEX [IX_Bookings_GroupId] ON [Bookings]([GroupId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_IdempotencyKey')
    CREATE UNIQUE INDEX [IX_Bookings_IdempotencyKey] ON [Bookings]([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_SlotId')
    CREATE INDEX [IX_Bookings_SlotId] ON [Bookings]([SlotId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_Status')
    CREATE INDEX [IX_Bookings_Status] ON [Bookings]([Status]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Bookings_UserId')
    CREATE INDEX [IX_Bookings_UserId] ON [Bookings]([UserId]);
GO
/* CHỐT 1: chống overbooking — 1 slot chỉ giữ 1 booking Active */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_court_slot_active')
    CREATE UNIQUE INDEX [ux_court_slot_active] ON [Bookings]([CourtId],[Date],[SlotId])
        WHERE [Status] IN (N'Pending', N'Confirmed');
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Branches_Name')
    CREATE UNIQUE INDEX [IX_Branches_Name] ON [Branches]([Name]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourtDateStatus_Date')
    CREATE INDEX [IX_CourtDateStatus_Date] ON [CourtDateStatus]([Date]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courts_BranchId')
    CREATE INDEX [IX_Courts_BranchId] ON [Courts]([BranchId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courts_BranchId_Code')
    CREATE UNIQUE INDEX [IX_Courts_BranchId_Code] ON [Courts]([BranchId],[Code]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GroupMembers_UserId')
    CREATE INDEX [IX_GroupMembers_UserId] ON [GroupMembers]([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_BookingId')
    CREATE INDEX [IX_Payments_BookingId] ON [Payments]([BookingId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_GatewayRef')
    CREATE UNIQUE INDEX [IX_Payments_GatewayRef] ON [Payments]([GatewayRef]) WHERE [GatewayRef] IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_Status_PaidAt')
    CREATE INDEX [IX_Payments_Status_PaidAt] ON [Payments]([Status],[PaidAt]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_UserId')
    CREATE INDEX [IX_Payments_UserId] ON [Payments]([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PlayGroups_OwnerId')
    CREATE INDEX [IX_PlayGroups_OwnerId] ON [PlayGroups]([OwnerId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PointLedger_BookingId')
    CREATE INDEX [IX_PointLedger_BookingId] ON [PointLedger]([BookingId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PointLedger_UserId_CreatedAt')
    CREATE INDEX [IX_PointLedger_UserId_CreatedAt] ON [PointLedger]([UserId],[CreatedAt]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_BookingId')
    CREATE UNIQUE INDEX [IX_Reviews_BookingId] ON [Reviews]([BookingId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_CourtId')
    CREATE INDEX [IX_Reviews_CourtId] ON [Reviews]([CourtId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_UserId')
    CREATE INDEX [IX_Reviews_UserId] ON [Reviews]([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TimeSlots_StartTime_EndTime')
    CREATE UNIQUE INDEX [IX_TimeSlots_StartTime_EndTime] ON [TimeSlots]([StartTime],[EndTime]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_BranchId')
    CREATE INDEX [IX_Users_BranchId] ON [Users]([BranchId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_PhoneNumber')
    CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [Users]([PhoneNumber]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_Role_Status')
    CREATE INDEX [IX_Users_Role_Status] ON [Users]([Role],[Status]);
GO
/* CHỐT 2: đúng MỘT admin Active (docs 02 §4) */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_active_admin')
    CREATE UNIQUE INDEX [ux_active_admin] ON [Users]([Role])
        WHERE [Status] = N'Active' AND [Role] = N'Admin';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherRedemptions_BookingId')
    CREATE INDEX [IX_VoucherRedemptions_BookingId] ON [VoucherRedemptions]([BookingId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherRedemptions_UserId')
    CREATE INDEX [IX_VoucherRedemptions_UserId] ON [VoucherRedemptions]([UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VoucherRedemptions_VoucherId_UserId')
    CREATE UNIQUE INDEX [IX_VoucherRedemptions_VoucherId_UserId] ON [VoucherRedemptions]([VoucherId],[UserId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vouchers_Code')
    CREATE UNIQUE INDEX [IX_Vouchers_Code] ON [Vouchers]([Code]);
GO

/* ============================================================================
   3. SEED DATA
   ============================================================================ */
SET IDENTITY_INSERT [Branches] ON;
INSERT INTO [Branches]
    ([Id],[Name],[Address],[Description],[GeoLat],[GeoLng],[OpenTime],[CloseTime],[Status],[IsActive],[CreatedAt])
VALUES
    (1, N'Sân Cầu Lông Minh Long', N'123 Nguyen Van A, Ha Noi', N'Chi nhan thu cu (dev)', 21.0285, 105.8521, '06:00:00', '23:00:00', N'Active', 1, GETDATE());
SET IDENTITY_INSERT [Branches] OFF;
GO

SET IDENTITY_INSERT [TimeSlots] ON;
/* 17 khung giu' 06:00→22:00 (docs 01 §3); peak = bắt đầu ≥ 18:00 */
INSERT INTO [TimeSlots] ([Id],[Label],[StartTime],[EndTime],[IsPeak],[IsActive],[CreatedAt])
VALUES
    ( 1, N'06:00', '06:00:00', '07:00:00', 0, 1, GETDATE()),
    ( 2, N'07:00', '07:00:00', '08:00:00', 0, 1, GETDATE()),
    ( 3, N'08:00', '08:00:00', '09:00:00', 0, 1, GETDATE()),
    ( 4, N'09:00', '09:00:00', '10:00:00', 0, 1, GETDATE()),
    ( 5, N'10:00', '10:00:00', '11:00:00', 0, 1, GETDATE()),
    ( 6, N'11:00', '11:00:00', '12:00:00', 0, 1, GETDATE()),
    ( 7, N'12:00', '12:00:00', '13:00:00', 0, 1, GETDATE()),
    ( 8, N'13:00', '13:00:00', '14:00:00', 0, 1, GETDATE()),
    ( 9, N'14:00', '14:00:00', '15:00:00', 0, 1, GETDATE()),
    (10, N'15:00', '15:00:00', '16:00:00', 0, 1, GETDATE()),
    (11, N'16:00', '16:00:00', '17:00:00', 0, 1, GETDATE()),
    (12, N'17:00', '17:00:00', '18:00:00', 0, 1, GETDATE()),
    (13, N'18:00', '18:00:00', '19:00:00', 1, 1, GETDATE()),
    (14, N'19:00', '19:00:00', '20:00:00', 1, 1, GETDATE()),
    (15, N'20:00', '20:00:00', '21:00:00', 1, 1, GETDATE()),
    (16, N'21:00', '21:00:00', '22:00:00', 1, 1, GETDATE()),
    (17, N'22:00', '22:00:00', '23:00:00', 1, 1, GETDATE());
SET IDENTITY_INSERT [TimeSlots] OFF;
GO

SET IDENTITY_INSERT [Courts] ON;
INSERT INTO [Courts]
    ([Id],[BranchId],[Code],[Name],[Surface],[PriceLowPeak],[PricePeak],[WeekendSurcharge],[Capacity],[Amenities],[ImageUrl],[RatingAvg],[RatingCount],[IsActive],[CreatedAt])
VALUES
    (1, 1, N'S01', N'San 1 (Thuong)',     N'Sindalo', 100000, 150000, 0.2000, 2, N'["gieng mat"]', NULL, 4.5, 12, 1, GETDATE()),
    (2, 1, N'S02', N'San 2 (Cao cap)',    N'Carpet',  150000, 220000, 0.3000, 4, N'["may lanh","gieng mat"]', NULL, 4.8, 30, 1, GETDATE()),
    (3, 1, N'S03', N'San 3 (Tre)',        N'Wood',    120000, 180000, 0.2000, 2, NULL, NULL, 4.2, 8,  1, GETDATE()),
    (4, 1, N'S04', N'San 4 (Ngoai tru)',  N'Wood',     80000, 120000, 0.1500, 2, NULL, NULL, 4.0, 5,  1, GETDATE()),
    (5, 1, N'S05', N'San 5 (Dao tao)',    N'Sindalo',  90000, 130000, 0.2000, 4, N'["gieng mat"]', NULL, 4.6, 22, 1, GETDATE()),
    (6, 1, N'S06', N'San 6 (Sua da)',     N'Carpet',  150000, 220000, 0.3000, 4, N'["may lanh"]', NULL, 0, 0, 0, GETDATE());
SET IDENTITY_INSERT [Courts] OFF;
GO

SET IDENTITY_INSERT [Vouchers] ON;
/* Duh' hanh giu' 1 nam (muc hien tai: 2026). */
INSERT INTO [Vouchers]
    ([Id],[Code],[Type],[Value],[MaxValue],[MinSpend],[MaxPerUser],[TotalQty],[UsedQty],[ValidFrom],[ValidUntil],[IsActive],[CreatedAt])
VALUES
    (1, N'WELCOME10',  N'Percent', 10, 50000,  200000, 1,   100,  0, '2026-01-01', '2026-12-31', 1, GETDATE()),
    (2, N'FIXED50K',   N'Fixed',   50000, NULL,  300000, 1,   200,  0, '2026-01-01', '2026-12-31', 1, GETDATE()),
    (3, N'WEEKEND20',  N'Percent', 20, 100000, 400000, 2,  1000,  0, '2026-01-01', '2026-12-31', 1, GETDATE()),
    (4, N'MEMBER10',   N'Percent', 10, NULL,     0,      5,  5000,  0, '2026-01-01', '2026-12-31', 1, GETDATE());
SET IDENTITY_INSERT [Vouchers] OFF;
GO

/* ---- 4 tai khoan (mat khau chung: Hovaten123@) ----
   BCrypt hash (da verify): $2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG
   Chi 1 Admin Active — de phui index ux_active_admin. */
SET IDENTITY_INSERT [Users] ON;
INSERT INTO [Users]
    ([Id],[FullName],[PhoneNumber],[Email],[PasswordHash],[AvatarUrl],[Role],[BranchId],[Status],[IsVerified],[CreatedAt])
VALUES
    (1, N'Customer 03', N'0900000003', N'customer03@dev.vn', N'$2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG', NULL, N'Customer', NULL, N'Active', 1, GETDATE()),
    (2, N'Customer 04', N'0900000004', N'customer04@dev.vn', N'$2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG', NULL, N'Customer', NULL, N'Active', 1, GETDATE()),
    (3, N'Manager Minh Long', N'0900000005', N'manager@dev.vn', N'$2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG', NULL, N'Manager', 1, N'Active', 1, GETDATE()),
    (4, N'Tran Admin', N'0900000006', N'admin@dev.vn', N'$2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG', NULL, N'Admin', NULL, N'Active', 1, GETDATE());
SET IDENTITY_INSERT [Users] OFF;
GO

-- EF migrations history (de update-database bao DB la up-to-date).
-- EF Core 8 quan doi cot ProductVersion (nvarchar(32), khong NULL).
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261013131702_InitialCreate')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId],[ProductVersion])
    VALUES (N'20261013131702_InitialCreate', N'8.0.28');
GO

/* ============================================================================
   4. VERIFY — cho de'm kiem tra nhanh
   ============================================================================ */
SELECT N'Users' AS T, COUNT(*) AS Total FROM [Users]
UNION ALL SELECT N'Branches', COUNT(*) FROM [Branches]
UNION ALL SELECT N'Courts', COUNT(*) FROM [Courts]
UNION ALL SELECT N'TimeSlots', COUNT(*) FROM [TimeSlots]
UNION ALL SELECT N'Vouchers', COUNT(*) FROM [Vouchers];

SELECT [Id],[FullName],[PhoneNumber],[Role],[Status] FROM [Users] ORDER BY [Id];
GO
