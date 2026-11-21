using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankStatementConverter.Infrastructure.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingEntryArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceVersion = table.Column<int>(type: "int", nullable: false),
                    DossierVersion = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Encoding = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PreviewFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntryArchives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Logo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoContentType = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LogoVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Photo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PhotoContentType = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PhotoVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ICE = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IF = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RC = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntryOutputTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryOutputTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StandardStorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GeneratedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementVersion = table.Column<int>(type: "int", nullable: false),
                    TemplateNameSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Encoding = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Encoding = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NumberCulture = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IncludeHeader = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractionProfileActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionProfileActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AliasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ModifiedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingFileDeletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingFileDeletions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatementActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SessionVersion = table.Column<int>(type: "int", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Journal = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClientEntryModelSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientEntryModelSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientEntryModelSets_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportTemplateField",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceField = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportTemplateField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportTemplateField_ExportTemplates_ExportTemplateId",
                        column: x => x.ExportTemplateId,
                        principalTable: "ExportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserSessionVersion = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceSessions_WorkspaceUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "WorkspaceUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FiledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ModifiedByName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StatementNumber = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    NormalizedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceStorageKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImportDiagnosticsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawExtractionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementImports_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatementImports_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementTreatments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementTreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementTreatments_StatementImports_StatementId",
                        column: x => x.StatementId,
                        principalTable: "StatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "EntryOutputTemplates",
                columns: new[] { "Id", "Active", "CreatedAt", "Description", "Name", "OutputJson", "UpdatedAt", "Version" },
                values: new object[] { new Guid("9d2e4a78-fb36-4820-9c04-87b370743915"), true, new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "N° compte, Intitulé, Libellé, Banque, Date, Débit HT et Crédit HT après calcul de la TVA.", "Écriture comptable S100", "{\"fileExtension\":\"txt\",\"delimiter\":\";\",\"encoding\":\"utf-8\",\"numberCulture\":\"fr-FR\",\"dateFormat\":\"dd/MM/yyyy\",\"includeHeader\":true,\"columns\":[{\"field\":\"ACCOUNT\",\"label\":\"N\\u00B0 compte\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"TITLE\",\"label\":\"Intitul\\u00E9\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"DESCRIPTION\",\"label\":\"Libell\\u00E9\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"BANK\",\"label\":\"Banque\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"DATE\",\"label\":\"Date\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"DEBIT_HT\",\"label\":\"D\\u00E9bit HT\",\"display\":true,\"export\":true,\"format\":null,\"required\":false},{\"field\":\"CREDIT_HT\",\"label\":\"Cr\\u00E9dit HT\",\"display\":true,\"export\":true,\"format\":null,\"required\":false}]}", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_ClientId_CreatedAt",
                table: "AccountingEntryArchives",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_ClientId_StatementId_PreviewFingerprint",
                table: "AccountingEntryArchives",
                columns: new[] { "ClientId", "StatementId", "PreviewFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntryArchives_StatementId_CreatedAt",
                table: "AccountingEntryArchives",
                columns: new[] { "StatementId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BankId_AccountNumber",
                table: "BankAccounts",
                columns: new[] { "BankId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_ClientId",
                table: "BankAccounts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Banks_Code",
                table: "Banks",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientEntryModelSets_ClientId",
                table: "ClientEntryModelSets",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ICE",
                table: "Clients",
                column: "ICE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BankAccountId",
                table: "Documents",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ClientId_CreatedAt",
                table: "Documents",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_StatementId_CreatedAt",
                table: "ExportJobs",
                columns: new[] { "StatementId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportTemplateField_ExportTemplateId_Position",
                table: "ExportTemplateField",
                columns: new[] { "ExportTemplateId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionProfileActivities_ProfileId_CreatedAt",
                table: "ExtractionProfileActivities",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionProfiles_BankCode_Currency",
                table: "ExtractionProfiles",
                columns: new[] { "BankCode", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementActivities_CreatedAt_Id",
                table: "StatementActivities",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementActivities_StatementId_CreatedAt",
                table: "StatementActivities",
                columns: new[] { "StatementId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_BankAccountId_FiledAt",
                table: "StatementImports",
                columns: new[] { "BankAccountId", "FiledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_ClientId_CreatedAt",
                table: "StatementImports",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementImports_CreatedAt_Id",
                table: "StatementImports",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementTreatments_StatementId_ScopeId",
                table: "StatementTreatments",
                columns: new[] { "StatementId", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSessions_ExpiresAt",
                table: "WorkspaceSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSessions_UserId",
                table: "WorkspaceSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceUsers_Email",
                table: "WorkspaceUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingEntryArchives");

            migrationBuilder.DropTable(
                name: "ClientEntryModelSets");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "EntryOutputTemplates");

            migrationBuilder.DropTable(
                name: "ExportJobs");

            migrationBuilder.DropTable(
                name: "ExportTemplateField");

            migrationBuilder.DropTable(
                name: "ExtractionProfileActivities");

            migrationBuilder.DropTable(
                name: "ExtractionProfiles");

            migrationBuilder.DropTable(
                name: "PendingFileDeletions");

            migrationBuilder.DropTable(
                name: "StatementActivities");

            migrationBuilder.DropTable(
                name: "StatementTreatments");

            migrationBuilder.DropTable(
                name: "WorkspaceSessions");

            migrationBuilder.DropTable(
                name: "ExportTemplates");

            migrationBuilder.DropTable(
                name: "StatementImports");

            migrationBuilder.DropTable(
                name: "WorkspaceUsers");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
