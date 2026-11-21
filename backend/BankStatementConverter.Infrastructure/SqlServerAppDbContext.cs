using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace BankStatementConverter.Infrastructure;

public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options, IHttpContextAccessor? http = null)
    : AppDbContext(options, http);

public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqlServerAppDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ALIAS_SQLSERVER") ?? AliasDatabase.LocalSqlServer).Options);
}

public static class AliasDatabase
{
    public const string LocalSqlServer = @"Server=.\SQLEXPRESS;Database=AliasConvertPdf;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";

    public static void AddAliasDatabase(this IServiceCollection services, string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configurez ConnectionStrings:Default.");
        // Existing installations can keep running on their old database during the transfer.
        var settings = new DbConnectionStringBuilder { ConnectionString = connection };
        if (settings.ContainsKey("Host"))
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
        else
            services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options => options.UseSqlServer(connection));
    }

    public static Task LockResource(this AppDbContext db, string resource, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=30000;
                IF @result < 0 THROW 51000, 'Verrou transactionnel indisponible.', 1;
                """, ct)
            : db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({resource}, 0))", ct);

    public static IQueryable<T> LockedRow<T>(this AppDbContext db, Guid id, bool forUpdate = true) where T : class
    {
        var table = db.Model.FindEntityType(typeof(T))!.GetTableName()!;
        var query = db.Database.IsSqlServer()
            ? $"SELECT * FROM [{table}] WITH ({(forUpdate ? "UPDLOCK, HOLDLOCK" : "HOLDLOCK")}) WHERE [Id] = {{0}}"
            : $"SELECT * FROM \"{table}\" WHERE \"Id\" = {{0}} FOR {(forUpdate ? "UPDATE" : "SHARE")}";
        return db.Set<T>().FromSqlRaw(query, id);
    }
}
