using System.Text;
using System.Text.Json;
using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.API;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Accounting;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
static Task<bool> Authorized(string action, params string[] permissions) => AuthorizedController("AccountingEntryModels", action, "POST", permissions);
static async Task<bool> AuthorizedController(string controller, string action, string method, params string[] permissions) {
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(permissions.Select(p => new Claim("permission", p)), "test")) };
    http.Request.Method = method;
    var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action };
    var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());
    await new PermissionFilter().OnAuthorizationAsync(context);return context.Result == null;
}
Check(await Authorized("Preview", "documents.read"), "API_ReadOnlyPreviewAllowed");
Check(!await AuthorizedController("ClientWorkspace", "History", "GET", "documents.read") && await AuthorizedController("ClientWorkspace", "History", "GET", "history.read"), "History_ActivityRequiresDedicatedPermission");
Check(!await AuthorizedController("ExtractionProfiles", "History", "GET", "documents.read") && await AuthorizedController("ExtractionProfiles", "History", "GET", "history.read"), "History_ProfileActivityRequiresDedicatedPermission");
Check(!typeof(AccountingEntryModelsController).Assembly.GetTypes().Any(x => x.Name == "AccountingController"), "RemovalApi_NoJournalPlanController");
Check(await AuthorizedController("StatementTreatments", "Get", "GET", "documents.read") && !await AuthorizedController("StatementTreatments", "Get", "GET", "documents.write"), "Treatment_ReadRequiresDocumentPermission");
Check(!await Authorized("Save", "documents.read"), "API_SaveRequiresWritePermission");
Check(!await Authorized("Export", "documents.read") && await Authorized("Export", "documents.export"), "API_ExportRequiresExportPermission");
Check(!await Authorized("ModelFile", "documents.read") && await Authorized("ModelFile", "documents.export") && !await Authorized("ArchiveFile", "documents.read") && await Authorized("ArchiveFile", "documents.export"), "API_IndependentFilesRequireExportPermission");
Check(await AuthorizedController("EntryOutputTemplates", "List", "GET", "documents.read") && await AuthorizedController("EntryOutputTemplates", "List", "GET", "templates.read") && !await AuthorizedController("EntryOutputTemplates", "List", "GET", "documents.write"), "EntryCatalog_ReadPermissionsAllowDocumentSelector");
Check(!await AuthorizedController("EntryOutputTemplates", "Update", "PUT", "documents.write") && await AuthorizedController("EntryOutputTemplates", "Update", "PUT", "templates.write"), "EntryCatalog_EditRequiresTemplateWrite");
Check(!await AuthorizedController("EntryOutputTemplates", "Download", "GET", "templates.read") && await AuthorizedController("EntryOutputTemplates", "Download", "GET", "documents.export"), "EntryCatalog_DownloadRequiresExportPermission");
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(args[0])).Build();
await LocalEntryStorageFixture.Verify();
var connection = config.GetConnectionString("Default")!;
if (new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connection }.ContainsKey("Host"))
{
    await RemovalMigrationChecks.Run(connection);
    var schema = "alias_entry_model_test_" + Guid.NewGuid().ToString("N");
    await using var admin = new NpgsqlConnection(connection);
    await admin.OpenAsync();
    await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin).ExecuteNonQueryAsync();
    try
    {
        var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped.ConnectionString).Options);
        await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        await RunFixtures(db);
    }
    finally { await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin).ExecuteNonQueryAsync(); }
}
else
{
    var scratch = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection) { InitialCatalog = "AliasChecks_" + Guid.NewGuid().ToString("N"), Pooling = false };
    await using var db = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>().UseSqlServer(scratch.ConnectionString).Options);
    var creator = db.GetService<IRelationalDatabaseCreator>();
    await creator.CreateAsync();
    try { await db.Database.MigrateAsync(); await RunFixtures(db); }
    finally { await creator.DeleteAsync(); }
}

static async Task RunFixtures(AppDbContext db)
{
    var client = new Client { Name = "Test client A", ICE = "111111111111111" }; var other = new Client { Name = "Test client B", ICE = "222222222222222" };
    var bank = new Bank { Name = "Test bank", Code = "TEST" };
    var account = new BankAccount { ClientId = client.Id, BankId = bank.Id, AccountNumber = "123456789012345678901234", AccountName = "Test bank account", Journal = "BQ1", AccountCode = "514100", Currency = "MAD" };
    var foreign = new BankAccount { ClientId = other.Id, BankId = bank.Id, AccountNumber = "223456789012345678901234", AccountName = "Other account", Journal = "BQ2", AccountCode = "514200" };
    var template = AccountingExportTemplates.Create("SAGEX3ENTRIES");
    var movement = new StatementTransaction { Sequence = 1, Date = new(2026, 11, 1), Description = "ACHAT FOURNISSEUR CAFE", Debit = 120, Reference = "T001", Currency = "MAD" };
    var extract = new StatementExtract(Guid.NewGuid(), "ACHAT", DateTime.UtcNow, 1, "MAD", [movement], 120, 0, -120);
    var source = new BankStatement { BankName = bank.Name, BankCode = bank.Code, AccountNumber = account.AccountNumber, Currency = "MAD", StatementNumber = "TEST-1",
        PeriodStart = new(2026, 11, 1), PeriodEnd = new(2026, 11, 30), OpeningBalance = 1000, ClosingBalance = 880, TotalDebit = 120, TotalCredit = 0,
        BalanceConvention = "CREDIT_INCREASES", Transactions = [movement], Extracts = [extract] };
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var record = new StatementImport { ClientId = client.Id, BankAccountId = account.Id, BankName = bank.Name, Currency = "MAD", StatementNumber = "TEST-1", Status = "VALIDATED", Version = 1, TransactionCount = 1, SourceFileName = "test.pdf", NormalizedJson = JsonSerializer.Serialize(source, json) };
    db.AddRange(client, other, bank, account, foreign, template, record); await db.SaveChangesAsync();
    var fees = source with { Transactions = [
        new StatementTransaction { Sequence = 1, Date = new(2026, 11, 1), Description = "COMMISSION SUR VIREMENT FOURNISSEUR", Debit = 150 },
        new StatementTransaction { Sequence = 2, Date = new(2026, 11, 1), Description = "TVA COMMISSION SUR VIREMENT FOURNISSEUR", Debit = 15 },
        new StatementTransaction { Sequence = 3, Date = new(2026, 11, 1), Description = "OPERATION NON IDENTIFIEE", Debit = 25 }
    ] };
    var run = new EntryModelRun(client.Id, record.Id, Guid.Empty, "TEST", BalancedScope: "fees");
    var issues = new List<AccountingIssue>();
    BalancedBankEntries.Build(fees, account, run, issues);
    Check(issues.Count == 1, "BalancedBank_VatRequiresConfirmation");
    run = run with { CounterAccounts = new Dictionary<Guid, string> { [fees.Transactions[1].Id] = "34552100" } };
    issues.Clear();
    var (bankRows, _) = BalancedBankEntries.Build(fees, account, run, issues);
    Check(issues.Count == 0 && bankRows.Length == 3 && bankRows.Select(r => r.Reference).Distinct().Count() == 1 &&
        bankRows.Sum(r => r.DebitHt) == 165 && bankRows.Sum(r => r.CreditHt) == 165, "BalancedBank_FeeAndVatShareOneBalancedPiece");
    var file = EntryModelTextExport.Generate(BalancedBankEntries.Format, bankRows);
    var textLines = EntryModelTextExport.EncodingFor(BalancedBankEntries.Format).GetString(file)
        .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    var fields = textLines.Select(line => System.Text.RegularExpressions.Regex.Split(line, " {2,}")).ToArray();
    Check(fields.Length == 3 && fields.All(line => line.Length == 7 && line[1] == "011126" && line[3] == "1")
        && fields[2][5] == "0,00" && fields[2][6] == "165,00", "BalancedBank_TxtPreservesColumnsAndAmounts");
    Check(fields[0][0] == "BQ1" && fields[0][2] == "61473000" && fields[0][5] == "150,00"
        && textLines.All(line => !line.Contains('\t') && line.Length == textLines[0].Length),
        "BalancedBank_AlignedTxtWithoutHeader");
    var fee = fees.Transactions[0];
    var ttcRun = run with { FeeVatRates = new Dictionary<Guid, decimal> { [fee.Id] = 10 } };
    var (alreadyItemized, _) = BalancedBankEntries.Build(fees, account, ttcRun, []);
    Check(alreadyItemized.Sum(r => r.DebitHt) == 165 &&
        alreadyItemized.Single(r => r.AccountCode == "34552100").DebitHt == 15,
        "BalancedBank_DoesNotDuplicateRecordedVat");
    var changedRate = run with { DefaultCommissionVatRate = 14 };
    var (recalculated, _) = BalancedBankEntries.Build(fees, account, changedRate, []);
    Check(recalculated.Length == 3 && recalculated[0].DebitHt == 144.74m &&
        recalculated[1].DebitHt == 20.26m && recalculated[2].CreditHt == 165m,
        "BalancedBank_ChangedGlobalRateRecalculatesRecordedVatWithoutChangingBankTotal");
    var (overridden, _) = BalancedBankEntries.Build(fees, account,
        changedRate with { FeeVatRates = new Dictionary<Guid, decimal> { [fee.Id] = 20 } }, []);
    Check(overridden.Length == 3 && overridden[0].DebitHt == 137.50m &&
        overridden[1].DebitHt == 27.50m && overridden[2].CreditHt == 165m,
        "BalancedBank_IndividualRateOverridesGlobalRateForRecordedVat");
    var observedTax = fees.Transactions[1] with { Sequence = 1, Debit = 22.50m,
        Description = "TVA SUR COMMISSION IMPAYE LCN REMISE SOUS REFERENCE 265\n434814" };
    var observedFee = fee with { Sequence = 2, Debit = 225,
        Description = "COMMISSION IMPAYEE LCN A L ENCAISSEMENT REMISE SOUS REF\nERNCE 265434814" };
    var (bmce, _) = BalancedBankEntries.Build(fees with { Transactions = [observedTax, observedFee] }, account, ttcRun, []);
    Check(bmce.Select(row => row.AccountCode).SequenceEqual(["61473000", "34552100", account.AccountCode])
        && bmce.Select(row => row.Reference).Distinct().Count() == 1 && bmce[2].CreditHt == 247.50m,
        "BalancedBank_BmceVatDirectlyBelowFeeAndBeforeBank");
    var global = ttcRun with { FeeVatRates = null, DefaultCommissionVatRate = 10 };
    var (globalRows, _) = BalancedBankEntries.Build(fees with { Transactions = [fee with { Debit = 110m }] }, account, global, []);
    Check(globalRows.Length == 3 && globalRows[1].DebitHt == 10 && globalRows[2].CreditHt == 110,
        "BalancedBank_GlobalCommissionVat");
    var stampFee = fee with { Description = "FRAIS TIMBRE SUR LCN", Debit = 110m };
    var (stampRows, stampMovements) = BalancedBankEntries.Build(fees with { Transactions = [stampFee] }, account,
        global with { FeeVatRates = new Dictionary<Guid, decimal> { [stampFee.Id] = 10 } }, []);
    Check(stampRows.Length == 2 && stampRows[0].DebitHt == 110 &&
        stampMovements.Single().AccountCategory == "fee", "BalancedBank_StampFeesNeverUseCommissionVat");
    var (excludedVat, _) = BalancedBankEntries.Build(fees with { Transactions = [fee] }, account,
        global with { FeeVatRates = new Dictionary<Guid, decimal> { [fee.Id] = 0 } }, []);
    Check(excludedVat.Length == 2, "BalancedBank_LineOverrideDisablesGlobalVat");
    var secondFee = fee with { Id = Guid.NewGuid(), Sequence = 3 };
    var secondVat = fees.Transactions[1] with { Id = Guid.NewGuid(), Sequence = 4 };
    var (adjacentPairs, _) = BalancedBankEntries.Build(
        fees with { Transactions = [fee, fees.Transactions[1], secondFee, secondVat] },
        account, global with { ConfirmDefaultVat = true }, []);
    Check(adjacentPairs.Length == 6 && adjacentPairs[2].CreditHt == 165 && adjacentPairs[5].CreditHt == 165,
        "BalancedBank_AdjacentVatDisambiguatesRepeatedCommissions");
    var (individualRows, individualMovements) = BalancedBankEntries.Build(
        fees with { Transactions = [fee, fees.Transactions[1], secondFee, secondVat] },
        account, global with { DefaultCommissionVatRate = 0, CounterAccounts = new Dictionary<Guid, string> { [secondVat.Id] = "34552100" } }, []);
    Check(individualMovements.Single(m => m.TransactionId == secondFee.Id).LinkedVatId == secondVat.Id
        && individualRows.Single(r => r.TransactionId == secondVat.Id).Sequence == secondFee.Sequence
        && individualRows.Single(r => r.TransactionId == secondFee.Id && r.AccountCode == account.AccountCode).CreditHt == 165
        && individualRows.Single(r => r.TransactionId == fee.Id && r.AccountCode == account.AccountCode).CreditHt == 150,
        "BalancedBank_IndividualVatConfirmationOnlyAppliesToItsCommission");
    foreach (var gross in new[] { 110m, 121m })
    {
        var (split, _) = BalancedBankEntries.Build(fees with { Transactions = [fee with { Debit = gross }] }, account, ttcRun, []);
        Check(split.Length == 3 && split[1].AccountCode == "34552100" &&
            split[0].DebitHt + split[1].DebitHt == gross && split[2].CreditHt == gross &&
            split[1].DebitHt == gross / 11, "BalancedBank_RecalculatesVatFromConfirmedGross");
    }
    BalancedBankEntries.Build(fees, account, run with { BalancedScope = "all" }, issues);
    Check(issues.Count == 1, "BalancedBank_MissingCounterAccountBlocksFullExport");
    var fullRun = run with
    {
        BalancedScope = "all", DefaultCommissionVatRate = 20,
        CounterAccounts = new Dictionary<Guid, string>
        {
            [fees.Transactions[2].Id] = "44110000"
        }
    };
    issues.Clear();
    var (fullRows, fullMovements) = BalancedBankEntries.Build(fees, account, fullRun, issues);
    Check(issues.Count == 0 && fullMovements.Length == 3 && fullRows.Length == 5 &&
        fullRows[1].DebitHt == 27.50m && fullRows[2].CreditHt == 165m &&
        fullRows.Any(r => r.AccountCode == "44110000" && r.DebitHt == 25m) &&
        fullRows.GroupBy(row => row.Reference).All(piece => piece.Sum(row => row.DebitHt - row.CreditHt) == 0),
        "BalancedBank_FullStatementIncludesVatAndOtherBalancedMovements");
    var context = new ControllerContext { HttpContext = new DefaultHttpContext() };
    var dashboard = new DashboardController(db) { ControllerContext = context };
    Check(await dashboard.Get(default) is OkObjectResult, "Dashboard_StatementAndExportCounts");
    var workspace = new ClientWorkspaceController(db) { ControllerContext = context };
    var extracts = (OkObjectResult)await workspace.Extracts(default, client.Id);
    var result = JsonSerializer.SerializeToElement(extracts.Value, json);
    Check(result.GetProperty("total").GetInt32() == 1 &&
        result.GetProperty("items")[0].GetProperty("id").GetGuid() == extract.Id, "ClientWorkspace_SavedExtract");
    var storage = new MemoryStorage();
    var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test accountant")], "test")) } };
    var engine = new ExportEngine(new ExportSourceResolver(), new(), new(), new(), new(), new(), new());
    var statements = new StatementImportService(db, new StatementValidator(), engine, storage, http);
    var accounting = new EntryModelStore(db);
    // Retain generic export/archive coverage independently of the retired journal preparation module.
    var genericPreview = await statements.PreviewAsync(record.Id, template.Id, new(record.Version), default);
    var exported = await statements.ExportAsync(record.Id, template.Id, new(record.Version, genericPreview.TemplateFingerprint, AllowErrors: true), default);
    Check(exported.Content.Length > 0, "GenericExports_PreservedWithoutJournalPlans");
    await TreatmentChecks.Run(db, statements, record, template.Id, other.Id);
    await IndependentChecks.Run(db, accounting, statements, storage, http, client, account);
}
sealed class MemoryStorage : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public async Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct) { using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes, ct); var key = Guid.NewGuid().ToString("N"); Files[key] = bytes.ToArray(); return key; }
    public Stream Open(string key) => new MemoryStream(Files[key]);
    public void Delete(string key) => Files.Remove(key);
}

