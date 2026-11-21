using System.Globalization;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Accounting;

public static class BalancedBankEntries
{
    private const string DefaultVatAccount = "34552100";
    public static EntryModelOutput Format { get; } = new()
    {
        Delimiter = "\t", Encoding = "windows-1252", DateFormat = "ddMMyy", IncludeHeader = false,
        Columns = [new("JOURNAL", "Journal", Required: true), new("DATE", "Date", Required: true),
            new("ACCOUNT", "Compte", Required: true), new("PIECE", "N° de pièce", Required: true),
            new("DESCRIPTION", "Libellé"), new("DEBIT_HT", "Débit"), new("CREDIT_HT", "Crédit")]
    };

    public static (EntryResultRow[] Rows, EntryModelRow[] Movements) Build(BankStatement source, BankAccount bank,
        EntryModelRun run, List<AccountingIssue> errors)
    {
        Validation.Require(run.BalancedScope is "all" or "fees", "Périmètre comptable invalide.");
        Validation.Require(run.DefaultCommissionVatRate is >= 0 and <= 100, "Le taux global doit être compris entre 0 et 100.");
        bool ValidCode(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 32 && value.All(char.IsAsciiLetterOrDigit);
        if (!ValidCode(bank.AccountCode)) errors.Add(new("BANK_ACCOUNT", "Renseignez le compte général dans Banques et comptes."));
        if (!ValidCode(bank.Journal)) errors.Add(new("BANK_JOURNAL", "Renseignez le code journal dans Banques et comptes."));
        var rows = new List<EntryResultRow>();
        var movements = new List<EntryModelRow>();
        string PairKey(string description)
        {
            var label = EntryModelEngine.Normalize(description);
            var reference = Regex.Match(label, @"\bREF(?:\s*ER(?:E)?NCE)\s+((?:\d\s*){9})$");
            if (reference.Success && Regex.IsMatch(label, @"\bCOMMISSION IMPAYEE? LCN\b"))
                return "LCN-IMPAYE:" + reference.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal);
            return Regex.Replace(label, @"\b(TVA|SUR)\b", "").Replace(" ", "", StringComparison.Ordinal);
        }
        var ordered = source.Transactions.OrderBy(t => t.Sequence).ToArray();
        var positions = ordered.Select((transaction, index) => (transaction.Id, index)).ToDictionary(t => t.Id, t => t.index);
        var vatGroups = ordered.Where(t => Regex.IsMatch(
                EntryModelEngine.Normalize(t.Description ?? ""), @"^(TVA\b|FRAIS TVA)"))
            .ToLookup(t => (t.Date, Key: PairKey(t.Description ?? "")));
        var commissionGroups = ordered.Where(t => Regex.IsMatch(
                EntryModelEngine.Normalize(t.Description ?? ""), @"^COMM?ISS?IONS?\b"))
            .ToLookup(t => (t.Date, Key: PairKey(t.Description ?? "")));
        var linkedVat = new Dictionary<Guid, StatementTransaction>();
        foreach (var group in commissionGroups)
        {
            var fees = group.ToList();
            var taxes = vatGroups[group.Key].ToList();
            bool SameDirection(StatementTransaction fee, StatementTransaction tax) =>
                AccountingEngine.BankAmounts(source, fee) is { } feeAmounts &&
                AccountingEngine.BankAmounts(source, tax) is { } taxAmounts &&
                (feeAmounts.Debit > 0) == (taxAmounts.Debit > 0);
            foreach (var fee in fees.ToArray())
            {
                var following = taxes.FirstOrDefault(t => positions[t.Id] == positions[fee.Id] + 1 && SameDirection(fee, t));
                if (following == null) continue;
                linkedVat.Add(fee.Id, following);
                fees.Remove(fee);
                taxes.Remove(following);
            }
            if (fees.Count == 1 && taxes.Count == 1 && SameDirection(fees[0], taxes[0]))
                linkedVat.Add(fees[0].Id, taxes[0]);
        }
        decimal CommissionRate(Guid id) =>
            run.FeeVatRates?.GetValueOrDefault(id, run.DefaultCommissionVatRate) ?? run.DefaultCommissionVatRate;
        static decimal IncludedVat(decimal total, decimal rate) =>
            total - decimal.Round(total / (1 + rate / 100), 2, MidpointRounding.AwayFromZero);

        // A valid rate explicitly selects the VAT split for a matched commission.
        var vatSelectedByRate = linkedVat.Where(pair => CommissionRate(pair.Key) is > 0 and <= 100)
            .Select(pair => pair.Value.Id).ToHashSet();
        foreach (var transaction in ordered)
        {
            var label = EntryModelEngine.Normalize(transaction.Description ?? "");
            bool Has(string pattern) => Regex.IsMatch(label, pattern, RegexOptions.CultureInvariant);
            var tax = Has(@"\bTVA\b");
            var commission = Has(@"^COMM?ISS?IONS?\b");
            var fee = commission || Has(@"\b(COMMISSION|COMMISION|FRAIS)\b");
            var combined = tax && fee && !label.StartsWith("TVA ", StringComparison.Ordinal) &&
                !label.StartsWith("FRAIS TVA", StringComparison.Ordinal);
            if (run.BalancedScope == "fees" && !fee && !tax) continue;
            var amounts = AccountingEngine.BankAmounts(source, transaction);
            var proposed = combined ? "" : tax ? DefaultVatAccount : fee ? "61473000" :
                Has(@"\b(CPTE A CPTE|COMPTE A COMPTE)\b") ? "51150000" :
                Has(@"\bDIVIDENDES\b") ? "44650000" :
                Has(@"\bVIGNETTES?\b") ? "61673000" :
                Has(@"\bWAFASALAF\b") ? "61321200" :
                Has(@"\b(REMISE|VIR RECU|CHQ RECU)\b") && amounts?.Debit > 0 ? "34210000" :
                Has(@"\b(PAIEMENT DE VOTRE CHEQUE|VIREMENT VERS CLIENT|VIREMENTS VERS CLIENT)\b") && amounts?.Credit > 0 ? "44110000" : "";
            var rate = !commission ? 0
                : run.FeeVatRates != null && run.FeeVatRates.TryGetValue(transaction.Id, out var selectedRate)
                ? selectedRate
                : !tax ? run.DefaultCommissionVatRate : 0;
            var separateVat = vatGroups.Contains((transaction.Date, PairKey(transaction.Description ?? "")));
            var linked = linkedVat.GetValueOrDefault(transaction.Id);
            var linkedAmounts = linked == null ? null : AccountingEngine.BankAmounts(source, linked);
            var account = run.CounterAccounts?.GetValueOrDefault(transaction.Id)?.Trim() ??
                (combined && rate > 0 ? "61473000" : proposed);
            var category = combined ? (commission ? "mixed" : "fee") : tax ? "vat" : commission ? "commission" : fee ? "fee" :
                Has(@"\b(VIREMENTS?|VIR|VRT|SRBM)\b") ? "transfer" :
                Has(@"\b(CHEQUES?|CHQ|CH)\b") ? "cheque" : "other";
            var message = rate != 0 && (!fee || (tax && !combined) || rate is < 0 or > 100)
                ? "Le taux doit être compris entre 0 et 100 et s’appliquer à une commission TTC." :
                rate > 0 && (!separateVat || linked != null) && (account == DefaultVatAccount || bank.AccountCode == DefaultVatAccount)
                ? "Choisissez des comptes distincts pour la commission, la TVA et la banque." :
                combined && rate == 0 && run.CounterAccounts?.ContainsKey(transaction.Id) != true
                ? "Commission et TVA sur une même ligne. Vérifiez le justificatif. Indiquez un compte uniquement si le total doit aller sur ce compte." :
                tax && !run.ConfirmDefaultVat && !vatSelectedByRate.Contains(transaction.Id) && run.CounterAccounts?.ContainsKey(transaction.Id) != true
                    ? "Confirmez le compte de TVA après contrôle du justificatif, ou indiquez le compte de frais." :
                amounts == null || transaction.Date == null ? "Corrigez la date, le montant ou le sens du mouvement." :
                !ValidCode(account) || account == bank.AccountCode ? "Renseignez le compte de contrepartie." : "";
            movements.Add(new(transaction.Id, transaction.Sequence, transaction.Date, transaction.Description ?? "",
                message.Length == 0 ? "MATCHED" : "INVALID", [], null, amounts == null ? null : amounts.Value.Debit + amounts.Value.Credit,
                null, null, message, account, category, linked?.Id,
                linkedAmounts is { } vatAmounts ? vatAmounts.Debit + vatAmounts.Credit : null));
            if (message.Length > 0)
            {
                errors.Add(new("COUNTER_ACCOUNT", $"Ligne {transaction.Sequence} : {message}"));
                continue;
            }
            var (debit, credit) = amounts!.Value;
            // Each source movement gets its own balanced piece, including explicit VAT movements.
            var piece = (movements.Count).ToString(CultureInfo.InvariantCulture);
            var total = debit + credit;
            var vat = rate > 0 && !separateVat
                ? IncludedVat(total, rate)
                : 0;
            Add(account, credit > 0 ? credit - vat : 0, debit > 0 ? debit - vat : 0);
            if (vat > 0)
                Add(DefaultVatAccount, credit > 0 ? vat : 0, debit > 0 ? vat : 0,
                    commission ? "TVA sur commission / " : "TVA / ");
            Add(bank.AccountCode, debit, credit);
            void Add(string code, decimal dr, decimal cr, string prefix = "") =>
                rows.Add(new(transaction.Id, transaction.Sequence, transaction.Date, code, "", prefix + (transaction.Description ?? ""),
                    bank.AccountName, bank.Journal, bank.Currency, piece, dr + cr, dr + cr, 0, 0, dr, cr));
        }
        if (movements.Count == 0) errors.Add(new("EMPTY", "Aucun mouvement dans ce périmètre."));
        var categories = movements.ToDictionary(m => m.TransactionId, m => m.AccountCategory);
        var counterpartRows = rows.Where(r => r.AccountCode != bank.AccountCode)
            .ToLookup(r => r.TransactionId);
        var reservedVat = linkedVat.Values.Select(t => t.Id).ToHashSet();
        var pairs = new List<(EntryResultRow Fee, EntryResultRow Tax)>();
        foreach (var (feeId, tax) in linkedVat)
        {
            var feeRow = counterpartRows[feeId].FirstOrDefault(r => r.AccountCode != DefaultVatAccount);
            var taxRow = counterpartRows[tax.Id].FirstOrDefault(r => r.AccountCode == DefaultVatAccount);
            if (feeRow != null && taxRow != null) pairs.Add((feeRow, taxRow));
        }
        var groups = rows.Where(r => r.AccountCode != bank.AccountCode &&
                (categories[r.TransactionId] == "fee" && r.AccountCode != DefaultVatAccount ||
                 categories[r.TransactionId] == "vat" && r.AccountCode == DefaultVatAccount && !reservedVat.Contains(r.TransactionId)))
            .GroupBy(r => (r.Date, Label: PairKey(r.Description), Debit: r.DebitHt > 0));
        foreach (var group in groups)
        {
            var fees = group.Where(r => categories[r.TransactionId] == "fee").ToList();
            var taxes = group.Where(r => categories[r.TransactionId] == "vat").ToList();
            foreach (var fee in fees.ToArray())
            {
                var following = taxes.FirstOrDefault(t => positions[t.TransactionId] == positions[fee.TransactionId] + 1);
                if (following == null) continue;
                pairs.Add((fee, following));
                fees.Remove(fee);
                taxes.Remove(following);
            }
            if (fees.Count == 1 && taxes.Count == 1) pairs.Add((fees[0], taxes[0]));
        }
        foreach (var (feeRow, taxRow) in pairs)
        {
            var feeBank = rows.FindIndex(r => r.TransactionId == feeRow.TransactionId && r.AccountCode == bank.AccountCode);
            var taxBank = rows.Single(r => r.TransactionId == taxRow.TransactionId && r.AccountCode == bank.AccountCode);
            var bankRow = rows[feeBank];
            rows[feeBank] = bankRow with { DebitHt = bankRow.DebitHt + taxBank.DebitHt, CreditHt = bankRow.CreditHt + taxBank.CreditHt,
                Gross = bankRow.Gross + taxBank.Gross, Net = bankRow.Net + taxBank.Net };
            rows.Remove(taxBank);
            rows.Remove(taxRow);
            var feeIndex = rows.IndexOf(feeRow);
            var rate = linkedVat.ContainsKey(feeRow.TransactionId) ? CommissionRate(feeRow.TransactionId) : 0;
            var appliedTaxRow = taxRow;
            if (rate > 0)
            {
                var gross = feeRow.DebitHt + feeRow.CreditHt + taxRow.DebitHt + taxRow.CreditHt;
                var vat = IncludedVat(gross, rate);
                var net = gross - vat;
                var isDebit = feeRow.DebitHt > 0;
                rows[feeIndex] = feeRow with { Gross = net, Net = net, DebitHt = isDebit ? net : 0, CreditHt = isDebit ? 0 : net };
                appliedTaxRow = taxRow with { Gross = vat, Net = vat, DebitHt = isDebit ? vat : 0, CreditHt = isDebit ? 0 : vat };
            }
            rows.Insert(feeIndex + 1, appliedTaxRow with { Reference = feeRow.Reference, Sequence = feeRow.Sequence });
        }
        return (rows.OrderBy(r => r.Sequence).ThenBy(r => r.AccountCode == bank.AccountCode).ToArray(), movements.ToArray());
    }
}
