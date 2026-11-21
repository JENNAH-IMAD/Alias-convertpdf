using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Accounting;
public static class EntryModelTextExport
{
    public const int FormatVersion = 5;
    public const string AlignedSeparator = "  ";
    public static bool IsAlignedSage(EntryModelOutput output) =>
        output.FileExtension == "txt" && output.Delimiter == "\t" && !output.IncludeHeader &&
        output.Columns.Where(c => c.Export).Select(c => c.Field)
            .SequenceEqual(new[] { "JOURNAL", "DATE", "ACCOUNT", "PIECE", "DESCRIPTION", "DEBIT_HT", "CREDIT_HT" });
    public static readonly string[] Fields = ["ACCOUNT", "TITLE", "DESCRIPTION", "BANK", "NET", "GROSS", "TAX", "RATE", "DATE", "REFERENCE", "ABBREVIATION", "CURRENCY", "DEBIT_HT", "CREDIT_HT", "JOURNAL", "PIECE"];
    public static void Validate(EntryModelOutput output)
    {
        Validation.Require(output.FileExtension is "txt" or "csv" or "tsv", "Type de fichier invalide : TXT, CSV ou TSV attendu.");
        Validation.Require(output.Delimiter is ";" or "," or "\t" or "|", "Séparateur TXT invalide.");
        Validation.Require(output.Encoding is "utf-8" or "windows-1252", "Encodage TXT invalide.");
        Validation.Require(output.NumberCulture is "fr-FR" or "en-US", "Format des nombres invalide.");
        Validation.Require(string.IsNullOrEmpty(output.DateFormat) || DateFormats.Contains(output.DateFormat), "Format de date invalide.");
        Validation.Require(output.Columns is { Count: >= 1 and <= 12 } && output.Columns.All(c => c != null && Fields.Contains(c.Field) && !string.IsNullOrWhiteSpace(c.Label) && c.Label.Length <= 80 && !c.Label.Any(char.IsControl)), "Colonnes du modèle invalides.");
        Validation.Require(output.Columns.Select(c => c.Field).Distinct().Count() == output.Columns.Count, "Une colonne ne peut apparaître qu'une fois.");
        Validation.Require(output.Columns.All(c => ValidColumnFormat(c)), "Format de colonne invalide.");
        Validation.Require(output.Columns.Any(c => c.Display) && output.Columns.Any(c => c.Export), "Conservez au moins une colonne d'affichage et une colonne d'export.");
    }
    private static readonly string[] DateFormats = ["ddMMyy", "ddMMyyyy", "yyMMdd", "yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy"];
    private static readonly string[] NumberFormats = ["0.00", "0.000", "0.##"];
    private static readonly string[] Cultures = ["fr-FR", "en-US"];
    private static bool NumericField(string field) => field is "NET" or "DEBIT_HT" or "CREDIT_HT" or "GROSS" or "TAX" or "RATE";
    private static bool ValidColumnFormat(EntryModelColumn column)
    {
        if (string.IsNullOrEmpty(column.Format)) return true;
        if (column.Field == "DATE") return DateFormats.Contains(column.Format);
        if (!NumericField(column.Field)) return false;
        var parts = column.Format.Split(':', 3);
        return parts.Length == 3 && parts[0] == "number" && Cultures.Contains(parts[1]) && NumberFormats.Contains(parts[2]);
    }
    public static Encoding EncodingFor(EntryModelOutput output)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return output.Encoding == "utf-8" ? new UTF8Encoding(false, true) : Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
    public static string Value(EntryResultRow row, EntryModelColumn column, EntryModelOutput output)
    {
        var field = column.Field;
        var format = column.Format;
        var parts = format?.Split(':', 3);
        var culture = parts is { Length: 3 } && parts[0] == "number" ? parts[1] : output.NumberCulture;
        var numberFormat = parts is { Length: 3 } && parts[0] == "number" ? parts[2] : field == "RATE" ? "0.##" : "0.00";
        var number = CultureInfo.GetCultureInfo(culture);
        return field switch { "JOURNAL" => row.Journal ?? row.Abbreviation, "PIECE" => row.PieceNumber ?? row.Reference, "ACCOUNT" => row.AccountCode, "TITLE" => row.Title, "DESCRIPTION" => row.Description, "BANK" => row.Bank,
            "NET" => row.Net.ToString(numberFormat, number), "DEBIT_HT" => row.DebitHt.ToString(numberFormat, number), "CREDIT_HT" => row.CreditHt.ToString(numberFormat, number),
            "GROSS" => row.Gross.ToString(numberFormat, number), "TAX" => row.Tax.ToString(numberFormat, number), "RATE" => row.Rate.ToString(numberFormat, number),
            "DATE" => row.Date?.ToString(string.IsNullOrEmpty(format) ? output.DateFormat : format, CultureInfo.InvariantCulture) ?? "", "REFERENCE" => row.Reference, "ABBREVIATION" => row.Abbreviation, "CURRENCY" => row.Currency,
            _ => throw new AppException(400, "Colonne inconnue.") };
    }
    public static byte[] Generate(EntryModelOutput output, IReadOnlyList<EntryResultRow> rows, bool structureOnly = false)
    {
        Validate(output); var columns = output.Columns.Where(c => c.Export).ToArray();
        string Clean(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string Cell(string value)
        {
            // PDF labels can wrap over several lines. A text export keeps one physical line per movement.
            var singleLine = Clean(value);
            return singleLine.Contains(output.Delimiter, StringComparison.Ordinal) || singleLine.Contains('"')
                ? "\"" + singleLine.Replace("\"", "\"\"") + "\"" : singleLine;
        }
        var lines = new List<string>();
        if (output.IncludeHeader || structureOnly) lines.Add(string.Join(output.Delimiter, columns.Select(c => Cell(c.Label))));
        if (!structureOnly)
        {
            var aligned = IsAlignedSage(output);
            var cells = new List<string[]>();
            foreach (var row in rows)
            {
                foreach (var column in columns.Where(c => c.Required))
                    if (string.IsNullOrWhiteSpace(Value(row, column, output)))
                        throw new AppException(422, $"La colonne « {column.Label} » est obligatoire, mais une ligne ne contient pas de valeur.");
                if (aligned) cells.Add(columns.Select(c => Clean(Value(row, c, output))).ToArray());
                else lines.Add(string.Join(output.Delimiter, columns.Select(c => Cell(Value(row, c, output)))));
            }
            if (cells.Count > 0)
            {
                var widths = columns.Select((column, i) => Math.Max(column.Label.Length, cells.Max(row => row[i].Length))).ToArray();
                lines.AddRange(cells.Select(row => string.Join(AlignedSeparator,
                    row.Select((cell, i) => i >= 5 ? cell.PadLeft(widths[i]) : cell.PadRight(widths[i])))));
            }
        }
        try { return EncodingFor(output).GetBytes(lines.Count > 0 ? string.Join("\r\n", lines) + "\r\n" : ""); }
        catch (EncoderFallbackException) { throw new AppException(400, "Des caractères ne sont pas représentables dans l'encodage choisi. Sélectionnez UTF-8."); }
    }
}
