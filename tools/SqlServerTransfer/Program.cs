using System.Data;
using System.Data.Common;
using System.Text.Json;
using BankStatementConverter.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

if (args.Length != 2 || args[0] is not ("copy" or "verify"))
    throw new ArgumentException("SqlServerTransfer copy|verify <configuration PostgreSQL source>. La destination provient de ALIAS_SQLSERVER.");
var copy = args[0] == "copy";
using var config = JsonDocument.Parse(File.ReadAllText(args[1]));
var connections = config.RootElement.GetProperty("ConnectionStrings");
var source = Environment.GetEnvironmentVariable("ALIAS_POSTGRES") ??
    (connections.TryGetProperty("LegacyPostgres", out var legacy) ? legacy : connections.GetProperty("Default")).GetString();
var target = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALIAS_SQLSERVER") ?? AliasDatabase.LocalSqlServer);
if (string.IsNullOrWhiteSpace(target.InitialCatalog) || new[] { "master", "model", "msdb", "tempdb" }.Contains(target.InitialCatalog, StringComparer.OrdinalIgnoreCase))
    throw new ArgumentException("Indiquez une base applicative de destination.");
await using var pg = new NpgsqlConnection(source);
await pg.OpenAsync();
await using var snapshot = await pg.BeginTransactionAsync(IsolationLevel.RepeatableRead);
await using var sqlContext = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>().UseSqlServer(target.ConnectionString).Options);
var entities = sqlContext.Model.GetEntityTypes().ToArray();
var tables = new List<string>();
while (tables.Count < entities.Length)
{
    var next = entities.Where(e => !tables.Contains(e.GetTableName()!) &&
        e.GetForeignKeys().All(f => tables.Contains(f.PrincipalEntityType.GetTableName()!))).ToArray();
    if (next.Length == 0) throw new InvalidOperationException("Dépendances cycliques entre tables.");
    tables.AddRange(next.Select(e => e.GetTableName()!));
}
var pgTables = await Names(pg, snapshot, "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'");
if (!pgTables.ToHashSet().SetEquals(tables)) throw new InvalidOperationException("Le schéma PostgreSQL ne correspond pas au modèle applicatif. Copie annulée.");
await using (var locks = new NpgsqlCommand("LOCK TABLE " + string.Join(", ", tables.Select(t => $"public.\"{t}\"")) + " IN SHARE MODE NOWAIT", pg, snapshot))
    await locks.ExecuteNonQueryAsync();
var creator = sqlContext.GetService<IRelationalDatabaseCreator>();
if (copy)
{
    if (await creator.ExistsAsync()) throw new InvalidOperationException("La copie exige un nouveau nom de base. Pour contrôler une copie existante, utilisez verify.");
    await creator.CreateAsync();
    await sqlContext.Database.MigrateAsync();
}
await using var sql = new SqlConnection(target.ConnectionString);
await sql.OpenAsync();
await using var transaction = (SqlTransaction)await sql.BeginTransactionAsync(IsolationLevel.Serializable);
var sqlTables = await Names(sql, transaction, "SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name <> '__EFMigrationsHistory'");
if (!sqlTables.ToHashSet().SetEquals(tables)) throw new InvalidOperationException("Tables SQL Server inattendues.");
if (copy)
{
    // The initial migration installs one format. The source catalog is authoritative, including deletions.
    await using var removeSeed = new SqlCommand("DELETE FROM dbo.EntryOutputTemplates WHERE Id=@id", sql, transaction);
    removeSeed.Parameters.AddWithValue("@id", BankStatementConverter.Infrastructure.Accounting.EntryOutputTemplateCatalog.DefaultId);
    await removeSeed.ExecuteNonQueryAsync();
}
long total = 0;
foreach (var table in tables)
{
    var columns = await Names(pg, snapshot, $"SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='{table}' ORDER BY ordinal_position");
    var destination = await Names(sql, transaction, $"SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.[{table}]')");
    if (!columns.ToHashSet().SetEquals(destination)) throw new InvalidOperationException($"Colonnes différentes pour {table}.");
    string pgSelect = $"SELECT {string.Join(", ", columns.Select(c => $"\"{c}\""))} FROM public.\"{table}\" ORDER BY \"Id\"";
    string sqlSelect = $"SELECT {string.Join(", ", columns.Select(c => $"[{c}]"))} FROM dbo.[{table}] ORDER BY CONVERT(char(36), Id) COLLATE Latin1_General_100_BIN2";
    if (copy)
    {
        await using var read = new NpgsqlCommand(pgSelect, pg, snapshot) { CommandTimeout = 300 };
        await using var rows = await read.ExecuteReaderAsync();
        using var bulk = new SqlBulkCopy(sql, SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints, transaction)
            { DestinationTableName = $"dbo.[{table}]", BatchSize = 1000, BulkCopyTimeout = 300 };
        foreach (var column in columns) bulk.ColumnMappings.Add(column, column);
        await bulk.WriteToServerAsync(rows);
    }
    await using var sourceRows = new NpgsqlCommand(pgSelect, pg, snapshot) { CommandTimeout = 300 };
    await using var targetRows = new SqlCommand(sqlSelect, sql, transaction) { CommandTimeout = 300 };
    await using var left = await sourceRows.ExecuteReaderAsync();
    await using var right = await targetRows.ExecuteReaderAsync();
    long count = 0;
    while (await left.ReadAsync())
    {
        if (!await right.ReadAsync()) throw new InvalidDataException($"{table} : ligne manquante.");
        for (int i = 0; i < columns.Count; i++)
        {
            var a = left.GetValue(i);
            var b = right.GetValue(i);
            var equal = (a, b) switch
            {
                (byte[] x, byte[] y) => x.AsSpan().SequenceEqual(y),
                (DateOnly x, DateTime y) => x == DateOnly.FromDateTime(y),
                _ => Equals(a, b)
            };
            if (!equal) throw new InvalidDataException($"{table}.{columns[i]} : valeur différente à la ligne {count + 1}.");
        }
        count++;
    }
    if (await right.ReadAsync()) throw new InvalidDataException($"{table} : lignes supplémentaires.");
    Console.WriteLine($"{table}: {count}");
    total += count;
}
await transaction.CommitAsync();
await snapshot.CommitAsync();
Console.WriteLine($"{tables.Count} tables, {total} lignes, toutes les valeurs vérifiées.");

static async Task<List<string>> Names(DbConnection db, DbTransaction transaction, string query)
{
    await using var command = db.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = query;
    await using var reader = await command.ExecuteReaderAsync();
    var names = new List<string>();
    while (await reader.ReadAsync()) names.Add(reader.GetString(0));
    return names;
}
