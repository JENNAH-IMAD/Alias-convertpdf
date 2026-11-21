using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Exports;
using BankStatementConverter.Application.Exports;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

namespace BankStatementConverter.API;

internal static class ServiceRegistration
{
    public static void AddAliasServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddAliasDatabase(builder.Configuration.GetConnectionString("Default"));
        builder.Services.AddControllers(options => options.Filters.Add<PermissionFilter>())
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString);

        AddWorkspaceSecurity(builder);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Alias Convert PDF", Version = "v1" }));
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3002"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition")));

        builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = null);
        builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = long.MaxValue);
        AddCatalogServices(builder);
        AddConversionServices(builder);
    }

    // Les clés persistent hors du binaire pour conserver les sessions après une mise à jour.
    private static void AddWorkspaceSecurity(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<WorkspaceUserService>();
        builder.Services.AddScoped<WorkspaceCookieEvents>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
        {
            o.Cookie.Name = "Alias.Session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = false;
            o.EventsType = typeof(WorkspaceCookieEvents);
        });
        builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        var storageRoot = Path.GetFullPath(builder.Configuration["Storage:Root"] ?? "uploads", builder.Environment.ContentRootPath);
        var keysDirectory = Path.Combine(storageRoot, ".keys");
        Directory.CreateDirectory(keysDirectory);
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysDirectory)).SetApplicationName("Alias.Workspace");
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("authentication", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }

    private static void AddCatalogServices(WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue("ArchiveCleanup:Enabled", true))
            builder.Services.AddHostedService<ArchiveCleanupWorker>();
        builder.Services.AddScoped<BankService>();
        builder.Services.AddSingleton<IFileStorageService, LocalFileStorage>();
        builder.Services.AddScoped(typeof(CatalogService<,,>));
        builder.Services.AddScoped<ICatalogDefinition<Client, ClientInput, ClientDto>, ClientDefinition>();
        builder.Services.AddScoped<ICatalogDefinition<BankAccount, BankAccountInput, BankAccountDto>, AccountDefinition>();
        builder.Services.AddScoped<ICatalogDefinition<ExportTemplate, ExportInput, ExportDto>, ExportDefinition>();
    }

    private static void AddConversionServices(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IExportSourceResolver, ExportSourceResolver>();
        builder.Services.AddSingleton<ExportDateFormatter>();
        builder.Services.AddSingleton<ExportNumberFormatter>();
        builder.Services.AddSingleton<CsvExportWriter>();
        builder.Services.AddSingleton<DelimitedTextExportWriter>();
        builder.Services.AddSingleton<ExportFileNameBuilder>();
        builder.Services.AddSingleton<StatementValidator>();
        builder.Services.AddSingleton<IExportEngine, ExportEngine>();
        builder.Services.AddScoped<StatementImportService>();
        builder.Services.AddScoped<StatementTreatmentStore>();
        builder.Services.AddScoped<BankStatementConverter.Infrastructure.Accounting.EntryModelStore>();
        builder.Services.AddScoped<BankStatementConverter.Infrastructure.Accounting.IndependentEntryModelService>();
        builder.Services.AddScoped<BankStatementConverter.Infrastructure.Accounting.EntryOutputTemplateCatalog>();
        builder.Services.AddSingleton<ProcessingProgressStore>();
        builder.Services.AddScoped<RequestProcessingProgress>();
        builder.Services.AddScoped<IProcessingProgress>(s => s.GetRequiredService<RequestProcessingProgress>());
        builder.Services.AddSingleton<StatementPdfExtractor>();
        builder.Services.AddSingleton<IStatementPdfExtractor>(s => s.GetRequiredService<StatementPdfExtractor>());
        builder.Services.AddSingleton<OcrWorkQueue>();
        builder.Services.AddHttpClient("paddle-local", http => http.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers();
        builder.Services.AddScoped<IPdfOcrClient>(s => new QueuedPdfOcrClient(
            new PaddleOcrClient(s.GetRequiredService<IHttpClientFactory>().CreateClient("paddle-local"), builder.Configuration),
            s.GetRequiredService<OcrWorkQueue>(), s.GetRequiredService<IProcessingProgress>()));
        builder.Services.AddScoped<IAutomaticPdfExtraction>(s => new HybridPdfExtraction(
            s.GetRequiredService<StatementPdfExtractor>(), s.GetRequiredService<IPdfOcrClient>(),
            builder.Configuration, s.GetRequiredService<IProcessingProgress>()));
        builder.Services.AddScoped<PdfStatementImportService>();
    }
}
