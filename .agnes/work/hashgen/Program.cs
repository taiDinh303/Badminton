using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Data.SqlClient;

var sql = File.ReadAllText(@"D:\BadmintonBooking\BadmintonBooking.sql");
// LocalDB is via trusted connection (Windows auth); server name for LocalDB is "(localdb)\MSSQLLocalDB"
var connStr = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

// Split the script on GO (as SQL Server does)
var batches = new List<string>();
var sb = new StringBuilder();
foreach (var line in sql.Split('\n'))
{
    var trimmed = line.Trim().ToUpperInvariant();
    if (trimmed == "GO" || trimmed == "GO;")
    {
        if (sb.Length > 0) batches.Add(sb.ToString());
        sb.Clear();
    }
    else sb.AppendLine(line);
}
if (sb.Length > 0) batches.Add(sb.ToString());

using var conn = new SqlConnection(connStr);
conn.Open();

int ok = 0;
foreach (var b in batches)
{
    if (string.IsNullOrWhiteSpace(b)) continue;
    using var cmd = new SqlCommand(b, conn);
    cmd.CommandTimeout = 120;
    try { cmd.ExecuteNonQuery(); ok++; }
    catch (SqlException ex)
    {
        Console.WriteLine($"BATCH #{ok} FAILED: {ex.Message}");
        Console.WriteLine("---- BATCH BODY ----");
        Console.WriteLine(b);
        Environment.ExitCode = 1;
        break;
    }
}

// Switch into BadmintonBooking and verify
using var conn2 = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=BadmintonBooking;Trusted_Connection=True;TrustServerCertificate=True;");
conn2.Open();
using var chk = new SqlCommand(@"
SELECT N'Users' AS T, COUNT(*) AS Total FROM [Users]
UNION ALL SELECT N'Branches', COUNT(*) FROM [Branches]
UNION ALL SELECT N'Courts', COUNT(*) FROM [Courts]
UNION ALL SELECT N'TimeSlots', COUNT(*) FROM [TimeSlots]
UNION ALL SELECT N'Vouchers', COUNT(*) FROM [Vouchers]
UNION ALL SELECT N'EFMigrationsHistory', COUNT(*) FROM [__EFMigrationsHistory];", conn2);
using var rd = chk.ExecuteReader();
while (rd.Read()) Console.WriteLine($"  {rd[0]} = {rd[1]}");
rd.Close();

using var chk2 = new SqlCommand(@"
SELECT [Id],[PhoneNumber],[Role],[Status] FROM [Users] ORDER BY [Id];", conn2);
using var rd2 = chk2.ExecuteReader();
while (rd2.Read()) Console.WriteLine($"  user {rd2[0]} {rd2[1]} {rd2[2]} {rd2[3]}");
rd2.Close();

// Verify BCrypt for the shared hash
var hash = "$2a$11$y0HD89.43iXVJJMCn.mMBuCpk7wKXKHQ1l4ycvjVcIBjiWC7XD7eG";
Console.WriteLine($"Hovaten123@ verifies: {BCrypt.Net.BCrypt.Verify("Hovaten123@", hash)}");
Console.WriteLine($"Batches executed OK: {ok}/{batches.Count}");
