using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace BankStatementConverter.API;

[ApiController]
public class ClientWorkspaceController(AppDbContext db) : ControllerBase
{
    [HttpGet("/api/client-workspace/results")]
    public async Task<IActionResult> Results(CancellationToken ct, Guid? clientId = null, int page = 1,
        int pageSize = 20, string? search = null, Guid? exportTemplateId = null, Guid? entryOutputTemplateId = null)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        Validation.Require(search == null || search.Length <= 150, "La recherche est limitée à 150 caractères.");
        var statements = db.StatementImports.AsNoTracking().Where(s => !clientId.HasValue || s.ClientId == clientId);
        var entries = db.AccountingEntryArchives.AsNoTracking().Where(a => !clientId.HasValue || a.ClientId == clientId);
        var jobs = db.ExportJobs.AsNoTracking();
        if (exportTemplateId.HasValue)
        {
            jobs = jobs.Where(j => j.TemplateId == exportTemplateId);
            entries = entries.Where(_ => false);
        }
        if (entryOutputTemplateId.HasValue)
        {
            jobs = jobs.Where(_ => false);
            var id = entryOutputTemplateId.Value;
            if (db.Database.IsSqlServer())
            {
                var matching = db.AccountingEntryArchives.FromSqlInterpolated($"SELECT * FROM AccountingEntryArchives WHERE TRY_CONVERT(uniqueidentifier, JSON_VALUE(SnapshotJson, '$.model.outputTemplateId'))={id}");
                entries = entries.Where(a => matching.Any(m => m.Id == a.Id));
            }
            else
            {
                var json = JsonSerializer.Serialize(new { model = new { outputTemplateId = id } });
                entries = entries.Where(a => EF.Functions.JsonContains(a.SnapshotJson, json));
            }
        }
        var legacy = from j in jobs
                     join s in statements on j.StatementId equals s.Id
                     select new { j.Id, j.StatementId, Kind = "export", SourceName = s.SourceFileName ?? s.BankName,
                         Model = j.TemplateNameSnapshot, j.FileName, j.Encoding, j.Delimiter, HasFile = j.StorageKey != null, j.CreatedAt };
        var accounting = entries.Select(a => new { a.Id, a.StatementId, Kind = "entry", SourceName = a.SourceFileName,
            Model = a.ModelName, a.FileName, a.Encoding, a.Delimiter, HasFile = a.StorageKey != "", a.CreatedAt });
        var treatments = db.Database.IsSqlServer()
            ? db.StatementTreatments.FromSqlRaw("SELECT * FROM StatementTreatments WHERE JSON_VALUE(SnapshotJson, '$.kind')='ENTRY'")
            : db.StatementTreatments.FromSqlRaw("""SELECT * FROM "StatementTreatments" WHERE "SnapshotJson"->>'kind'='ENTRY'""");
        if (exportTemplateId.HasValue) treatments = treatments.Where(_ => false);
        if (entryOutputTemplateId.HasValue)
        {
            var id = entryOutputTemplateId.Value;
            var filter = JsonSerializer.Serialize(new { selection = new { outputTemplateId = id } });
            treatments = db.Database.IsSqlServer()
                ? db.StatementTreatments.FromSqlInterpolated($"""
                    SELECT t.* FROM StatementTreatments t
                    WHERE JSON_VALUE(t.SnapshotJson, '$.kind')='ENTRY'
                    AND (TRY_CONVERT(uniqueidentifier, JSON_VALUE(t.SnapshotJson, '$.selection.outputTemplateId'))={id}
                    OR (JSON_VALUE(t.SnapshotJson, '$.selection.outputTemplateId') IS NULL AND EXISTS (
                        SELECT 1 FROM ClientEntryModelSets d CROSS APPLY OPENJSON(d.ModelsJson)
                        WITH (ModelId uniqueidentifier '$.id', FormatId uniqueidentifier '$.outputTemplateId') m
                        WHERE d.ClientId=TRY_CONVERT(uniqueidentifier, JSON_VALUE(t.SnapshotJson, '$.clientId'))
                        AND m.ModelId=TRY_CONVERT(uniqueidentifier, JSON_VALUE(t.SnapshotJson, '$.selection.modelId'))
                        AND m.FormatId={id})))
                    """)
                : treatments.Where(t => EF.Functions.JsonContains(t.SnapshotJson, filter));
        }
        var previews = from t in treatments
                       join s in statements on t.StatementId equals s.Id
                       select new { Id = t.ScopeId, StatementId = s.Id, Kind = "preview", SourceName = s.SourceFileName ?? s.BankName,
                           Model = "Écritures comptables", FileName = "", Encoding = "", Delimiter = "", HasFile = false, CreatedAt = t.UpdatedAt };
        var query = legacy.Concat(accounting).Concat(previews);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.SourceName.ToLower().Contains(term) || r.Model.ToLower().Contains(term)
                || r.FileName.ToLower().Contains(term));
        }
        return Ok(new
        {
            total = await query.CountAsync(ct),
            items = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.StatementId).ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct),
            page, pageSize
        });
    }

    [HttpGet("/api/client-workspace/model-filters")]
    public async Task<IActionResult> ModelFilters(CancellationToken ct)
    {
        var entryFormats = await db.EntryOutputTemplates.AsNoTracking()
            .Where(template => template.Active)
            .Select(template => new WorkspaceModelFilterOption(
                template.Id, "entry-output", template.Name))
            .ToListAsync(ct);
        var exportModels = await db.ExportTemplates.AsNoTracking()
            .Where(template => template.IsActive)
            .Select(template => new WorkspaceModelFilterOption(
                template.Id, "export", template.Name))
            .ToListAsync(ct);
        return Ok(entryFormats.Concat(exportModels)
            .OrderBy(model => model.Kind).ThenBy(model => model.Label));
    }

    [HttpGet("/api/client-workspace/{clientId:guid}/statements")]
    [HttpGet("/api/client-workspace/statements")]
    public async Task<IActionResult> Statements(CancellationToken ct, Guid? clientId = null, int page = 1, int pageSize = 20, bool unassigned = false, string? status = null, string? search = null, string? conversionType = null, Guid? exportTemplateId = null, Guid? entryOutputTemplateId = null)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        Validation.Require(status == null || status is "VALIDATED" or "NEEDS_REVIEW" or "EXPORTED", "Statut invalide.");
        Validation.Require(search == null || search.Length <= 150, "La recherche est limitée à 150 caractères.");
        Validation.Require(conversionType == null || conversionType is "STATEMENT" or "EXTRACT", "Type de conversion invalide.");
        if (clientId.HasValue && !await db.Clients.AnyAsync(c => c.Id == clientId.Value, ct))
            throw new AppException(404, "Client introuvable.");
        var query = db.StatementImports.AsNoTracking().AsQueryable();
        if (conversionType == "EXTRACT")
            query = db.StatementImports.FromSqlRaw(db.Database.IsSqlServer() ? "SELECT * FROM StatementImports WHERE JSON_VALUE(NormalizedJson, '$.conversionType') = 'EXTRACT'" : "SELECT * FROM \"StatementImports\" WHERE \"NormalizedJson\"->>'conversionType' = 'EXTRACT'").AsNoTracking();
        if (conversionType == "STATEMENT")
            query = db.StatementImports.FromSqlRaw(db.Database.IsSqlServer() ? "SELECT * FROM StatementImports WHERE COALESCE(JSON_VALUE(NormalizedJson, '$.conversionType'), 'STATEMENT') = 'STATEMENT'" : "SELECT * FROM \"StatementImports\" WHERE COALESCE(\"NormalizedJson\"->>'conversionType', 'STATEMENT') = 'STATEMENT'").AsNoTracking();
        if (clientId.HasValue)
            query = query.Where(s => s.ClientId == clientId.Value);
        if (unassigned)
            query = query.Where(s => s.ClientId == null);
        if (status != null)
            query = query.Where(s => s.Status == status);
        if (exportTemplateId.HasValue)
            query = query.Where(s => db.ExportJobs.Any(job => job.StatementId == s.Id && job.TemplateId == exportTemplateId.Value));
        if (entryOutputTemplateId.HasValue)
        {
            var snapshotFilter = JsonSerializer.Serialize(new { model = new { outputTemplateId = entryOutputTemplateId.Value } });
            var matchingArchives = db.Database.IsSqlServer()
                ? db.AccountingEntryArchives.FromSqlInterpolated($"SELECT * FROM AccountingEntryArchives WHERE TRY_CONVERT(uniqueidentifier, JSON_VALUE(SnapshotJson, '$.model.outputTemplateId'))={entryOutputTemplateId.Value}")
                : db.AccountingEntryArchives.Where(a => EF.Functions.JsonContains(a.SnapshotJson, snapshotFilter));
            query = query.Where(s => matchingArchives.Any(a => a.StatementId == s.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.BankName.ToLower().Contains(term) || (s.SourceFileName != null && s.SourceFileName.ToLower().Contains(term)) ||
                (s.StatementNumber != null && s.StatementNumber.ToLower().Contains(term)) || db.Clients.Any(c => c.Id == s.ClientId && c.Name.ToLower().Contains(term)));
        }
        var items = await query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(s => new
        {
            s.Id,
            s.ClientId,
            s.BankAccountId,
            s.BankName,
            s.StatementNumber,
            s.Currency,
            s.SourceFileName,
            s.PeriodStart,
            s.PeriodEnd,
            s.Status,
            s.Version,
            s.TransactionCount,
            s.CreatedAt,
            s.UpdatedAt,
            s.ImportedByName,
            s.ModifiedByName,
            clientName = db.Clients.Where(c => c.Id == s.ClientId).Select(c => c.Name).FirstOrDefault(),
            accountName = db.BankAccounts.Where(a => a.Id == s.BankAccountId).Select(a => a.AccountName).FirstOrDefault(),
            accountNumber = db.BankAccounts.Where(a => a.Id == s.BankAccountId).Select(a => a.AccountNumber).FirstOrDefault(),
            exportCount = db.ExportJobs.Count(e => e.StatementId == s.Id),
            lastExportAt = db.ExportJobs.Where(e => e.StatementId == s.Id).Select(e => (DateTime?)e.CreatedAt).Max()
        }).ToListAsync(ct);
        return Ok(new
        {
            items,
            total = await query.CountAsync(ct),
            page,
            pageSize
        });
    }
    [HttpGet("/api/client-workspace/{clientId:guid}/extracts")]
    [HttpGet("/api/client-workspace/extracts")]
    public async Task<IActionResult> Extracts(CancellationToken ct, Guid? clientId = null, int page = 1, int pageSize = 20, string? search = null)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        Validation.Require(search == null || search.Length <= 150, "La recherche est limitée à 150 caractères.");
        if (clientId.HasValue && !await db.Clients.AnyAsync(c => c.Id == clientId, ct))
            throw new AppException(404, "Client introuvable.");
        var query = db.Database.SqlQuery<StatementExtractArchiveRow>(db.Database.IsSqlServer() ? (FormattableString)$"""
            SELECT s.Id AS StatementId, e.Id, e.FilterText, e.ExcludeText, e.CreatedAt,
                (SELECT COUNT(*) FROM OPENJSON(e.Transactions)) AS TransactionCount,
                e.TotalDebit, e.TotalCredit, e.Balance, e.Currency, s.BankName, s.SourceFileName, s.ClientId, s.BankAccountId,
                c.Name AS ClientName, a.AccountName, a.AccountNumber
            FROM StatementImports s CROSS APPLY OPENJSON(s.NormalizedJson, '$.extracts') WITH (
                Id uniqueidentifier '$.id', FilterText nvarchar(max) '$.filterText', ExcludeText nvarchar(max) '$.excludeText',
                CreatedAt datetime2 '$.createdAt', Transactions nvarchar(max) '$.transactions' AS JSON,
                TotalDebit decimal(38,10) '$.totalDebit', TotalCredit decimal(38,10) '$.totalCredit',
                Balance decimal(38,10) '$.balance', Currency nvarchar(32) '$.currency') e
            LEFT JOIN Clients c ON c.Id=s.ClientId LEFT JOIN BankAccounts a ON a.Id=s.BankAccountId
            WHERE ({clientId} IS NULL OR s.ClientId={clientId})
            """ : $"""
            SELECT s."Id" AS "StatementId", (e.value->>'id')::uuid AS "Id",
                e.value->>'filterText' AS "FilterText", e.value->>'excludeText' AS "ExcludeText", (e.value->>'createdAt')::timestamptz AS "CreatedAt",
                jsonb_array_length(e.value->'transactions') AS "TransactionCount",
                (e.value->>'totalDebit')::numeric AS "TotalDebit", (e.value->>'totalCredit')::numeric AS "TotalCredit",
                (e.value->>'balance')::numeric AS "Balance", e.value->>'currency' AS "Currency",
                s."BankName", s."SourceFileName", s."ClientId", s."BankAccountId",
                c."Name" AS "ClientName", a."AccountName", a."AccountNumber"
            FROM "StatementImports" s
            CROSS JOIN LATERAL jsonb_array_elements(COALESCE(s."NormalizedJson"->'extracts', '[]'::jsonb)) e(value)
            LEFT JOIN "Clients" c ON c."Id"=s."ClientId"
            LEFT JOIN "BankAccounts" a ON a."Id"=s."BankAccountId"
            WHERE ({clientId}::uuid IS NULL OR s."ClientId"={clientId})
            """);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FilterText.ToLower().Contains(term) || (x.ExcludeText != null && x.ExcludeText.ToLower().Contains(term)) || x.BankName.ToLower().Contains(term) || (x.ClientName != null && x.ClientName.ToLower().Contains(term)) || (x.SourceFileName != null && x.SourceFileName.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            items,
            total,
            page,
            pageSize
        });
    }
    [HttpGet("/api/client-workspace/history")]
    public async Task<IActionResult> History(CancellationToken ct, Guid? statementId = null, Guid? clientId = null, Guid? bankAccountId = null, int page = 1, int pageSize = 20, string? search = null, string? action = null, DateOnly? from = null, DateOnly? to = null)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var query = db.StatementActivities.AsNoTracking().AsQueryable();
        if (statementId.HasValue)
            query = query.Where(x => x.StatementId == statementId);
        if (clientId.HasValue)
            query = query.Where(x => x.ClientId == clientId);
        if (bankAccountId.HasValue)
            query = query.Where(x => x.BankAccountId == bankAccountId);
        query = HistoryActivityQuery.Apply(query, search, action, from, to);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            items,
            filterVersion = 1,
            total = await query.CountAsync(ct),
            page,
            pageSize
        });
    }
}

public sealed record WorkspaceModelFilterOption(Guid Id, string Kind, string Label);

public sealed class StatementExtractArchiveRow
{
    public Guid Id
    {
        get; set;
    }
    public Guid StatementId
    {
        get; set;
    }
    public Guid? ClientId
    {
        get; set;
    }
    public Guid? BankAccountId
    {
        get; set;
    }
    public string FilterText { get; set; } = "";
    public string? ExcludeText
    {
        get; set;
    }
    public string BankName { get; set; } = "";
    public string? SourceFileName
    {
        get; set;
    }
    public string? ClientName
    {
        get; set;
    }
    public string? AccountName
    {
        get; set;
    }
    public string? AccountNumber
    {
        get; set;
    }
    public string? Currency
    {
        get; set;
    }
    public DateTime CreatedAt
    {
        get; set;
    }
    public int TransactionCount
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
    public decimal TotalDebit
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
    public decimal TotalCredit
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
    public decimal Balance
    {
        get; set;
    }
}
