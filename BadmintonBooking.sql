/*
=============================================================
 BadmintonBooking - SQL Server Database
 Version: 1.0
 Purpose: Fresh database for the rebuilt Badminton Booking API
=============================================================

Run this script in SQL Server Management Studio (SSMS).

IMPORTANT:
- This script creates a NEW database named BadmintonBooking.
- If the database already exists, it will be dropped first.
- User passwords are intentionally NOT seeded here. The ASP.NET
  Core API should create users and hash passwords securely.
*/

USE master;
GO

IF DB_ID(N'BadmintonBooking') IS NOT NULL
BEGIN
    ALTER DATABASE [BadmintonBooking]
    SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    DROP DATABASE [BadmintonBooking];
END
GO

CREATE DATABASE [BadmintonBooking];
GO

USE [BadmintonBooking];
GO

/* =========================================================
   1. ROLES
   ========================================================= */
CREATE TABLE dbo.Roles
(
    RoleId          INT IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(50) NOT NULL,
    Description     NVARCHAR(255) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
);
GO

/* =========================================================
   2. USERS
   ========================================================= */
CREATE TABLE dbo.Users
(
    UserId          INT IDENTITY(1,1) NOT NULL,
    RoleId          INT NOT NULL,
    FullName        NVARCHAR(100) NOT NULL,
    Email           NVARCHAR(150) NOT NULL,
    PhoneNumber     VARCHAR(15) NULL,
    PasswordHash    NVARCHAR(500) NOT NULL,
    AvatarUrl       NVARCHAR(500) NULL,
    DateOfBirth     DATE NULL,
    Gender          VARCHAR(10) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0) NULL,

    CONSTRAINT PK_Users PRIMARY KEY (UserId),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId)
        REFERENCES dbo.Roles(RoleId),

    CONSTRAINT UQ_Users_Email UNIQUE (Email),

    CONSTRAINT CK_Users_Gender
        CHECK (Gender IS NULL OR Gender IN ('Male', 'Female', 'Other'))
);
GO

CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);
CREATE INDEX IX_Users_PhoneNumber ON dbo.Users(PhoneNumber);
GO

/* =========================================================
   3. COURT TYPES
   ========================================================= */
CREATE TABLE dbo.CourtTypes
(
    CourtTypeId     INT IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(50) NOT NULL,
    Description     NVARCHAR(255) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_CourtTypes_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_CourtTypes_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_CourtTypes PRIMARY KEY (CourtTypeId),
    CONSTRAINT UQ_CourtTypes_Name UNIQUE (Name)
);
GO

/* =========================================================
   4. COURTS
   ========================================================= */
CREATE TABLE dbo.Courts
(
    CourtId         INT IDENTITY(1,1) NOT NULL,
    CourtTypeId     INT NOT NULL,
    CourtCode       VARCHAR(20) NOT NULL,
    CourtName       NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    Location        NVARCHAR(255) NULL,
    PricePerHour    DECIMAL(18,2) NOT NULL,
    Status          VARCHAR(20) NOT NULL CONSTRAINT DF_Courts_Status DEFAULT 'Available',
    ImageUrl        NVARCHAR(500) NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_Courts_IsActive DEFAULT 1,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Courts_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0) NULL,

    CONSTRAINT PK_Courts PRIMARY KEY (CourtId),
    CONSTRAINT FK_Courts_CourtTypes FOREIGN KEY (CourtTypeId)
        REFERENCES dbo.CourtTypes(CourtTypeId),

    CONSTRAINT UQ_Courts_CourtCode UNIQUE (CourtCode),

    CONSTRAINT CK_Courts_PricePerHour
        CHECK (PricePerHour >= 0),

    CONSTRAINT CK_Courts_Status
        CHECK (Status IN ('Available', 'Maintenance', 'Inactive'))
);
GO

CREATE INDEX IX_Courts_CourtTypeId ON dbo.Courts(CourtTypeId);
CREATE INDEX IX_Courts_Status ON dbo.Courts(Status);
GO

/* =========================================================
   5. TIME SLOTS
   ========================================================= */
CREATE TABLE dbo.TimeSlots
(
    TimeSlotId      INT IDENTITY(1,1) NOT NULL,
    StartTime       TIME(0) NOT NULL,
    EndTime         TIME(0) NOT NULL,
    IsActive        BIT NOT NULL CONSTRAINT DF_TimeSlots_IsActive DEFAULT 1,

    CONSTRAINT PK_TimeSlots PRIMARY KEY (TimeSlotId),
    CONSTRAINT UQ_TimeSlots_Time UNIQUE (StartTime, EndTime),

    CONSTRAINT CK_TimeSlots_TimeRange
        CHECK (EndTime > StartTime)
);
GO

/* =========================================================
   6. BOOKINGS
   ========================================================= */
CREATE TABLE dbo.Bookings
(
    BookingId       INT IDENTITY(1,1) NOT NULL,
    BookingCode     VARCHAR(30) NOT NULL,
    UserId          INT NOT NULL,
    BookingDate     DATE NOT NULL,
    TotalAmount     DECIMAL(18,2) NOT NULL,
    Status          VARCHAR(20) NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT 'Pending',
    Note            NVARCHAR(500) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Bookings_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0) NULL,

    CONSTRAINT PK_Bookings PRIMARY KEY (BookingId),
    CONSTRAINT FK_Bookings_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId),

    CONSTRAINT UQ_Bookings_BookingCode UNIQUE (BookingCode),

    CONSTRAINT CK_Bookings_TotalAmount
        CHECK (TotalAmount >= 0),

    CONSTRAINT CK_Bookings_Status
        CHECK (Status IN ('Pending', 'Confirmed', 'Paid', 'Cancelled', 'Completed', 'Expired'))
);
GO

CREATE INDEX IX_Bookings_UserId ON dbo.Bookings(UserId);
CREATE INDEX IX_Bookings_BookingDate ON dbo.Bookings(BookingDate);
CREATE INDEX IX_Bookings_Status ON dbo.Bookings(Status);
CREATE INDEX IX_Bookings_Date_Status ON dbo.Bookings(BookingDate, Status);
GO

/* =========================================================
   7. BOOKING DETAILS
   BookingDate is intentionally stored here as well.
   It allows SQL Server to enforce slot uniqueness without
   needing a cross-table constraint.
   ========================================================= */
CREATE TABLE dbo.BookingDetails
(
    BookingDetailId INT IDENTITY(1,1) NOT NULL,
    BookingId       INT NOT NULL,
    CourtId         INT NOT NULL,
    TimeSlotId      INT NOT NULL,
    BookingDate     DATE NOT NULL,
    Price           DECIMAL(18,2) NOT NULL,
    Status          VARCHAR(20) NOT NULL CONSTRAINT DF_BookingDetails_Status DEFAULT 'Reserved',

    CONSTRAINT PK_BookingDetails PRIMARY KEY (BookingDetailId),

    CONSTRAINT FK_BookingDetails_Bookings FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings(BookingId),

    CONSTRAINT FK_BookingDetails_Courts FOREIGN KEY (CourtId)
        REFERENCES dbo.Courts(CourtId),

    CONSTRAINT FK_BookingDetails_TimeSlots FOREIGN KEY (TimeSlotId)
        REFERENCES dbo.TimeSlots(TimeSlotId),

    CONSTRAINT CK_BookingDetails_Price
        CHECK (Price >= 0),

    CONSTRAINT CK_BookingDetails_Status
        CHECK (Status IN ('Reserved', 'Cancelled', 'Completed'))
);
GO

CREATE INDEX IX_BookingDetails_BookingId
    ON dbo.BookingDetails(BookingId);

CREATE INDEX IX_BookingDetails_Court_Date
    ON dbo.BookingDetails(CourtId, BookingDate);

CREATE INDEX IX_BookingDetails_TimeSlot
    ON dbo.BookingDetails(TimeSlotId);
GO

/*
A slot must not be reserved twice.

We use a unique index for active/reserved details.
Cancelled details can be booked again.
*/
CREATE UNIQUE INDEX UX_BookingDetails_ActiveSlot
ON dbo.BookingDetails(CourtId, BookingDate, TimeSlotId)
WHERE Status = 'Reserved';
GO

/* =========================================================
   8. PAYMENTS
   ========================================================= */
CREATE TABLE dbo.Payments
(
    PaymentId       INT IDENTITY(1,1) NOT NULL,
    BookingId       INT NOT NULL,
    PaymentMethod   VARCHAR(30) NOT NULL,
    TransactionCode VARCHAR(100) NULL,
    Amount          DECIMAL(18,2) NOT NULL,
    Status          VARCHAR(20) NOT NULL CONSTRAINT DF_Payments_Status DEFAULT 'Pending',
    PaidAt          DATETIME2(0) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Payments PRIMARY KEY (PaymentId),

    CONSTRAINT FK_Payments_Bookings FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings(BookingId),

    CONSTRAINT CK_Payments_Amount
        CHECK (Amount >= 0),

    CONSTRAINT CK_Payments_Method
        CHECK (PaymentMethod IN ('Cash', 'BankTransfer', 'QRCode')),

    CONSTRAINT CK_Payments_Status
        CHECK (Status IN ('Pending', 'Paid', 'Failed', 'Refunded'))
);
GO

CREATE INDEX IX_Payments_BookingId ON dbo.Payments(BookingId);
CREATE INDEX IX_Payments_Status ON dbo.Payments(Status);
CREATE UNIQUE INDEX UX_Payments_TransactionCode
    ON dbo.Payments(TransactionCode)
    WHERE TransactionCode IS NOT NULL;
GO

/* =========================================================
   9. BANK ACCOUNTS
   ========================================================= */
CREATE TABLE dbo.BankAccounts
(
    BankAccountId      INT IDENTITY(1,1) NOT NULL,
    UserId             INT NULL,
    BankName           NVARCHAR(100) NOT NULL,
    AccountNumber      VARCHAR(50) NOT NULL,
    AccountHolderName  NVARCHAR(150) NOT NULL,
    IsDefault          BIT NOT NULL CONSTRAINT DF_BankAccounts_IsDefault DEFAULT 0,
    IsActive           BIT NOT NULL CONSTRAINT DF_BankAccounts_IsActive DEFAULT 1,
    CreatedAt          DATETIME2(0) NOT NULL CONSTRAINT DF_BankAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_BankAccounts PRIMARY KEY (BankAccountId),

    CONSTRAINT FK_BankAccounts_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId)
);
GO

CREATE INDEX IX_BankAccounts_UserId ON dbo.BankAccounts(UserId);
GO

/* =========================================================
   10. CHECK-INS
   ========================================================= */
CREATE TABLE dbo.CheckIns
(
    CheckInId       INT IDENTITY(1,1) NOT NULL,
    BookingId       INT NOT NULL,
    UserId          INT NOT NULL,
    CheckInTime     DATETIME2(0) NULL,
    CheckOutTime    DATETIME2(0) NULL,
    Status          VARCHAR(20) NOT NULL CONSTRAINT DF_CheckIns_Status DEFAULT 'Pending',
    Note            NVARCHAR(500) NULL,

    CONSTRAINT PK_CheckIns PRIMARY KEY (CheckInId),

    CONSTRAINT FK_CheckIns_Bookings FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings(BookingId),

    CONSTRAINT FK_CheckIns_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId),

    CONSTRAINT CK_CheckIns_Status
        CHECK (Status IN ('Pending', 'CheckedIn', 'CheckedOut', 'NoShow')),

    CONSTRAINT CK_CheckIns_Time
        CHECK (CheckOutTime IS NULL OR CheckInTime IS NULL OR CheckOutTime >= CheckInTime)
);
GO

CREATE UNIQUE INDEX UX_CheckIns_BookingId
    ON dbo.CheckIns(BookingId);

CREATE INDEX IX_CheckIns_UserId ON dbo.CheckIns(UserId);
GO

/* =========================================================
   11. REVIEWS
   ========================================================= */
CREATE TABLE dbo.Reviews
(
    ReviewId        INT IDENTITY(1,1) NOT NULL,
    UserId          INT NOT NULL,
    CourtId         INT NOT NULL,
    BookingId       INT NOT NULL,
    Rating          TINYINT NOT NULL,
    Comment         NVARCHAR(1000) NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0) NULL,

    CONSTRAINT PK_Reviews PRIMARY KEY (ReviewId),

    CONSTRAINT FK_Reviews_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId),

    CONSTRAINT FK_Reviews_Courts FOREIGN KEY (CourtId)
        REFERENCES dbo.Courts(CourtId),

    CONSTRAINT FK_Reviews_Bookings FOREIGN KEY (BookingId)
        REFERENCES dbo.Bookings(BookingId),

    CONSTRAINT CK_Reviews_Rating
        CHECK (Rating BETWEEN 1 AND 5)
);
GO

CREATE UNIQUE INDEX UX_Reviews_User_Booking_Court
    ON dbo.Reviews(UserId, BookingId, CourtId);
GO

/* =========================================================
   12. NOTIFICATIONS
   ========================================================= */
CREATE TABLE dbo.Notifications
(
    NotificationId INT IDENTITY(1,1) NOT NULL,
    UserId         INT NOT NULL,
    Title          NVARCHAR(200) NOT NULL,
    Message        NVARCHAR(1000) NOT NULL,
    Type           VARCHAR(30) NOT NULL,
    IsRead         BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
    CreatedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationId),

    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId)
);
GO

CREATE INDEX IX_Notifications_User_Read
    ON dbo.Notifications(UserId, IsRead, CreatedAt DESC);
GO

/* =========================================================
   13. SEED ROLES
   ========================================================= */
SET IDENTITY_INSERT dbo.Roles ON;

INSERT INTO dbo.Roles
(
    RoleId, Name, Description, IsActive
)
VALUES
(1, N'Admin',    N'Full system administrator', 1),
(2, N'Staff',    N'Badminton court staff',     1),
(3, N'Customer', N'Customer who books courts', 1);

SET IDENTITY_INSERT dbo.Roles OFF;
GO

/* =========================================================
   14. SEED COURT TYPES
   ========================================================= */
INSERT INTO dbo.CourtTypes
(
    Name, Description, IsActive
)
VALUES
(N'Standard', N'Standard badminton court', 1),
(N'VIP',      N'VIP badminton court',      1),
(N'Premium',  N'Premium badminton court',  1);
GO

/* =========================================================
   15. SEED TIME SLOTS
   ========================================================= */
INSERT INTO dbo.TimeSlots (StartTime, EndTime, IsActive)
VALUES
('06:00', '07:00', 1),
('07:00', '08:00', 1),
('08:00', '09:00', 1),
('09:00', '10:00', 1),
('10:00', '11:00', 1),
('11:00', '12:00', 1),
('12:00', '13:00', 1),
('13:00', '14:00', 1),
('14:00', '15:00', 1),
('15:00', '16:00', 1),
('16:00', '17:00', 1),
('17:00', '18:00', 1),
('18:00', '19:00', 1),
('19:00', '20:00', 1),
('20:00', '21:00', 1),
('21:00', '22:00', 1),
('22:00', '23:00', 1);
GO

/* =========================================================
   16. SEED COURTS
   ========================================================= */
INSERT INTO dbo.Courts
(
    CourtTypeId,
    CourtCode,
    CourtName,
    Description,
    Location,
    PricePerHour,
    Status,
    IsActive
)
VALUES
(1, 'STD-01', N'Sân Standard 01', N'Sân cầu lông tiêu chuẩn', N'Tầng 1', 80000,  'Available', 1),
(1, 'STD-02', N'Sân Standard 02', N'Sân cầu lông tiêu chuẩn', N'Tầng 1', 80000,  'Available', 1),
(1, 'STD-03', N'Sân Standard 03', N'Sân cầu lông tiêu chuẩn', N'Tầng 1', 80000,  'Available', 1),
(1, 'STD-04', N'Sân Standard 04', N'Sân cầu lông tiêu chuẩn', N'Tầng 1', 80000,  'Available', 1),
(2, 'VIP-01', N'Sân VIP 01',      N'Sân VIP',                 N'Tầng 2', 120000, 'Available', 1),
(2, 'VIP-02', N'Sân VIP 02',      N'Sân VIP',                 N'Tầng 2', 120000, 'Available', 1),
(3, 'PRE-01', N'Sân Premium 01',  N'Sân Premium',             N'Tầng 2', 150000, 'Available', 1),
(3, 'PRE-02', N'Sân Premium 02',  N'Sân Premium',             N'Tầng 2', 150000, 'Available', 1);
GO

/* =========================================================
   17. BASIC DATA CHECK
   ========================================================= */
SELECT 'Roles' AS TableName, COUNT(*) AS TotalRows FROM dbo.Roles
UNION ALL
SELECT 'CourtTypes', COUNT(*) FROM dbo.CourtTypes
UNION ALL
SELECT 'Courts', COUNT(*) FROM dbo.Courts
UNION ALL
SELECT 'TimeSlots', COUNT(*) FROM dbo.TimeSlots
UNION ALL
SELECT 'Users', COUNT(*) FROM dbo.Users
UNION ALL
SELECT 'Bookings', COUNT(*) FROM dbo.Bookings
UNION ALL
SELECT 'BookingDetails', COUNT(*) FROM dbo.BookingDetails
UNION ALL
SELECT 'Payments', COUNT(*) FROM dbo.Payments
UNION ALL
SELECT 'BankAccounts', COUNT(*) FROM dbo.BankAccounts
UNION ALL
SELECT 'CheckIns', COUNT(*) FROM dbo.CheckIns
UNION ALL
SELECT 'Reviews', COUNT(*) FROM dbo.Reviews
UNION ALL
SELECT 'Notifications', COUNT(*) FROM dbo.Notifications;
GO

PRINT '=============================================================';
PRINT 'BadmintonBooking database created successfully.';
PRINT 'Next step: create the ASP.NET Core solution and connect EF Core.';
PRINT '=============================================================';
GO
