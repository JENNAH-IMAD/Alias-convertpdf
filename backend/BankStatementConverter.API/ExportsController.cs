using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
namespace BankStatementConverter.API;

[Route("api/export-templates")]
public class ExportsController(CatalogService<ExportTemplate, ExportInput, ExportDto> service, AppDbContext db)
    : CatalogController<ExportTemplate, ExportInput, ExportDto>(service)
{
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var model = await db.ExportTemplates.AsNoTracking().Include(t => t.Fields).SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new AppException(404, "Modèle d’exportation introuvable.");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(model.Encoding);
        var separator = model.Delimiter == "\\t" ? "\t" : model.Delimiter;
        var fields = model.Fields.OrderBy(f => f.Position)
            .Select(f => "\"" + f.FieldName.Replace("\"", "\"\"") + "\"");
        return File(encoding.GetBytes(string.Join(separator, fields) + "\r\n"),
            $"text/plain; charset={encoding.WebName}", $"modele_export.{model.FileExtension}");
    }
}
