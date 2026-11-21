using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.SqlServer.Dac;
using System.Data.Common;

if (args is ["restore-sqlserver", var packagePath, var runtimeConfig])
{
    var settings = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(runtimeConfig)).Build();
    var destination = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(settings.GetConnectionString("Default"));
    if (string.IsNullOrWhiteSpace(destination.InitialCatalog) || new[] { "master", "model", "msdb", "tempdb" }.Contains(destination.InitialCatalog, StringComparer.OrdinalIgnoreCase))
        throw new ArgumentException("Indiquez la base applicative à restaurer.");
    using var package = BacPackage.Load(packagePath);
    new DacServices(destination.ConnectionString).ImportBacpac(package, destination.InitialCatalog);
    return;
}

if (args.Length != 3) throw new ArgumentException("Expected project root, output directory and pg_dump path.");
var root = Path.GetFullPath(args[0]);
var target = Path.GetFullPath(args[1]);
if (!target.StartsWith(Path.Combine(root, "output") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Snapshot output must stay inside project output.");
if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new InvalidOperationException("Use a fresh snapshot directory.");
var api = Path.Combine(root, "backend/BankStatementConverter.API");
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json")).AddJsonFile(Path.Combine(api, "appsettings.Development.json")).Build();
var connectionString = config.GetConnectionString("Default")!;
var postgres = new DbConnectionStringBuilder { ConnectionString = connectionString }.ContainsKey("Host");
var storage = Path.GetFullPath(config["Storage:Root"] ?? "uploads", api);
await using AppDbContext db = postgres
    ? new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options)
    : new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>().UseSqlServer(connectionString).Options);
var counts = new { users = await db.WorkspaceUsers.CountAsync(), clients = await db.Clients.CountAsync(), accounts = await db.BankAccounts.CountAsync(), statements = await db.StatementImports.CountAsync(), exports = await db.ExportJobs.CountAsync(), history = await db.StatementActivities.CountAsync(), exportModels = await db.ExportTemplates.CountAsync(), importModels = await db.ExtractionProfiles.CountAsync() };
Directory.CreateDirectory(target);
var dump = Path.Combine(target, postgres ? "database.dump" : "database.bacpac");
if (postgres)
{
    var connection = new NpgsqlConnectionStringBuilder(connectionString);
    var process = new ProcessStartInfo(Path.GetFullPath(args[2])) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
    foreach (var a in new[] { "--host", connection.Host!, "--port", connection.Port.ToString(), "--username", connection.Username!, "--dbname", connection.Database!, "--format=custom", "--no-owner", "--no-acl", "--exclude-table-data=public.\"WorkspaceSessions\"", "--exclude-table-data=public.\"PendingFileDeletions\"", "--file", dump }) process.ArgumentList.Add(a);
    process.Environment["PGPASSWORD"] = connection.Password!;
    using (var child = Process.Start(process)!) { await child.StandardError.ReadToEndAsync(); await child.WaitForExitAsync(); if (child.ExitCode != 0) throw new InvalidOperationException("Database export failed; original data unchanged."); }
}
else
{
    var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    var tables = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).Append("__EFMigrationsHistory")
        .Where(t => t is not ("WorkspaceSessions" or "PendingFileDeletions")).Select(t => Tuple.Create("dbo", t));
    new DacServices(connectionString).ExportBacpac(dump, connection.InitialCatalog, new DacExportOptions(), tables);
}
var uploads = Path.Combine(target, "uploads");
Directory.CreateDirectory(uploads);
var files = new List<object>();
foreach (var source in Directory.EnumerateFiles(storage, "*", SearchOption.AllDirectories))
{
    var relative = Path.GetRelativePath(storage, source);
    if (relative.Split(Path.DirectorySeparatorChar).Any(p => p.StartsWith('.')) || (File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
        continue;
    var destination = Path.GetFullPath(Path.Combine(uploads, relative));
    if (!destination.StartsWith(uploads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Invalid file location.");
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(source, destination, false);
    using var stream = File.OpenRead(destination);
    files.Add(new
    {
        path = relative.Replace('\\', '/'),
        sha256 = Convert.ToHexString(SHA256.HashData(stream))
    });
}
using (var dumpStream = File.OpenRead(dump)) File.WriteAllText(Path.Combine(target, "manifest.json"), JsonSerializer.Serialize(new { version = postgres ? 1 : 2, createdUtc = DateTime.UtcNow, counts, databaseSha256 = Convert.ToHexString(SHA256.HashData(dumpStream)), files }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { counts, files = files.Count, credentialsTransferred = false, sessionsTransferred = false }));
