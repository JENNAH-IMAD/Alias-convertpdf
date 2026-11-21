'use client';
import { useState } from 'react';
import { Button } from './ui/button';
import {
  type EntryLineEdit,
  defaultEntryColumnFormat,
  entryNumericFields,
  type EntryModelColumn,
  type EntryModelOutput,
  type EntryResultRow,
} from '@/services/entry-models';
const editableFields = {
  JOURNAL: 'journal',
  DATE: 'date',
  ACCOUNT: 'accountCode',
  PIECE: 'pieceNumber',
  DESCRIPTION: 'description',
  DEBIT_HT: 'debit',
  CREDIT_HT: 'credit',
} as const;
const numeric = entryNumericFields;

function formattedNumber(
  raw: string | null | undefined,
  column: EntryModelColumn,
  output: EntryModelOutput,
) {
  if (raw == null || raw === '') return '—';
  const setting =
    column.format ??
    defaultEntryColumnFormat(column.field, output.numberCulture, output.dateFormat);
  const [, culture = output.numberCulture, pattern = column.field === 'RATE' ? '0.##' : '0.00'] =
    setting.startsWith('number:') ? setting.split(':') : [];
  const decimals = pattern.split('.')[1] || '';
  const minimum = [...decimals].filter((digit) => digit === '0').length;
  const maximum = decimals.length;
  const source = String(raw).replace(',', '.');
  const negative = source.startsWith('-');
  const [wholeText = '0', fractionalText = ''] = (negative ? source.slice(1) : source).split('.');
  const fraction = fractionalText.padEnd(maximum + 1, '0');
  let wholeTextRounded = wholeText || '0';
  const fractionDigits = fraction.slice(0, maximum).split('');
  if (Number(fraction[maximum] || '0') >= 5) {
    let carry = 1;
    for (let i = fractionDigits.length - 1; i >= 0 && carry; i--) {
      const next = Number(fractionDigits[i] || '0') + carry;
      fractionDigits[i] = String(next % 10);
      carry = next > 9 ? 1 : 0;
    }
    if (carry) {
      const wholeDigits = wholeTextRounded.split('');
      for (let i = wholeDigits.length - 1; i >= 0 && carry; i--) {
        const next = Number(wholeDigits[i]) + carry;
        wholeDigits[i] = String(next % 10);
        carry = next > 9 ? 1 : 0;
      }
      wholeTextRounded = `${carry ? '1' : ''}${wholeDigits.join('')}`;
    }
  }
  const whole = wholeTextRounded.replace(
    /\B(?=(\d{3})+(?!\d))/g,
    culture === 'fr-FR' ? '\u202f' : ',',
  );
  let fractionOut = fractionDigits.join('');
  while (fractionOut.length > minimum && fractionOut.endsWith('0'))
    fractionOut = fractionOut.slice(0, -1);
  const decimalSeparator = culture === 'fr-FR' ? ',' : '.';
  return `${negative ? '-' : ''}${whole}${fractionOut ? decimalSeparator + fractionOut : ''}`;
}
export function EntryResultTable({
  output,
  rows,
  showEmpty = true,
  editing,
}: {
  output: EntryModelOutput;
  rows: EntryResultRow[];
  showEmpty?: boolean;
  editing?: {
    drafts: Record<number, EntryLineEdit>;
    busy: boolean;
    change: (index: number, draft: EntryLineEdit) => void;
    reset: () => void;
    save: () => void;
  };
}) {
  const [query, setQuery] = useState('');
  const [editMode, setEditMode] = useState(false);
  const columns = output.columns.filter((c) => c.display);
  const occurrences = new Map<string, number>();
  const keyedRows = rows.map((row, index) => {
    const identity = JSON.stringify([row.transactionId, row.accountCode]);
    const occurrence = occurrences.get(identity) ?? 0;
    occurrences.set(identity, occurrence + 1);
    return { row, index, key: `${identity}:${occurrence}` };
  });
  function formattedDate(raw: string | null | undefined, format: string) {
    if (!raw) return '—';
    const [year, month, day] = raw.slice(0, 10).split('-');
    if (!year || !month || !day) return raw;
    const values: Record<string, string> = { yyyy: year, yy: year.slice(-2), MM: month, dd: day };
    return format.replace(/yyyy|yy|MM|dd/g, (part) => values[part] ?? part);
  }
  function value(row: EntryResultRow, column: EntryModelColumn) {
    const field = column.field;
    const values: Record<string, string | null> = {
      JOURNAL: row.journal ?? row.abbreviation,
      PIECE: row.pieceNumber ?? row.reference,
      ACCOUNT: row.accountCode,
      TITLE: row.title,
      DESCRIPTION: row.description,
      BANK: row.bank,
      NET: row.net,
      DEBIT_HT: row.debitHt,
      CREDIT_HT: row.creditHt,
      GROSS: row.gross,
      TAX: row.tax,
      RATE: row.rate,
      DATE: row.date,
      REFERENCE: row.reference,
      ABBREVIATION: row.abbreviation,
      CURRENCY: row.currency,
    };
    const raw = values[field];
    if (numeric.has(field)) return formattedNumber(raw, column, output);
    return field === 'DATE'
      ? formattedDate(raw, column.format || output.dateFormat || 'dd/MM/yyyy')
      : raw || '—';
  }
  return (
    <div className="entry-model-table-wrap">
      <div className="mb-3 flex flex-wrap items-end gap-2">
        <label className="min-w-44 flex-1 text-sm">
          Filtrer les écritures
          <input
            className="ui-input mt-1"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Journal, compte, pièce, libellé…"
          />
        </label>
        {editing && (
          <>
            <Button
              variant="outline"
              size="sm"
              disabled={editing.busy}
              onClick={() => setEditMode(!editMode)}
            >
              {editMode ? 'Masquer la saisie' : 'Modifier les lignes'}
            </Button>
            {editMode && (
              <>
                <Button variant="outline" size="sm" disabled={editing.busy} onClick={editing.reset}>
                  Réinitialiser les corrections
                </Button>
                <Button size="sm" disabled={editing.busy} onClick={editing.save}>
                  Enregistrer et vérifier les lignes
                </Button>
              </>
            )}
          </>
        )}
      </div>
      <table
        className="entry-model-results-table entry-model-output-table"
        aria-label="Résultat du modèle d’écritures"
      >
        <thead>
          <tr>
            {columns.map((c) => (
              <th scope="col" key={c.field} className={numeric.has(c.field) ? 'amount' : ''}>
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {keyedRows
            .filter(({ row }) => {
              const values = columns.map((column) => value(row, column));
              return values.some((cell) =>
                cell.toLocaleLowerCase('fr').includes(query.trim().toLocaleLowerCase('fr')),
              );
            })
            .map(({ row, index, key }) => (
              <tr key={key}>
                {columns.map((c) => {
                  const field = editableFields[c.field as keyof typeof editableFields];
                  const draft = editing?.drafts[index] ?? {
                    transactionId: row.transactionId,
                    originalAccount: row.accountCode,
                    journal: row.journal ?? row.abbreviation,
                    date: row.date?.slice(0, 10) ?? '',
                    accountCode: row.accountCode,
                    pieceNumber: row.pieceNumber ?? row.reference,
                    description: row.description,
                    debit: row.debitHt,
                    credit: row.creditHt,
                  };
                  return (
                    <td
                      key={c.field}
                      data-label={c.label}
                      className={
                        numeric.has(c.field)
                          ? 'amount'
                          : c.field === 'ACCOUNT'
                            ? 'entry-model-output-account'
                            : ''
                      }
                    >
                      {editing && editMode && field ? (
                        <input
                          className="ui-input min-w-24"
                          type={field === 'date' ? 'date' : 'text'}
                          inputMode={numeric.has(c.field) ? 'decimal' : undefined}
                          aria-label={`${c.label} · ligne ${index + 1}`}
                          value={draft[field]}
                          disabled={editing.busy}
                          onChange={(event) =>
                            editing.change(index, { ...draft, [field]: event.target.value })
                          }
                        />
                      ) : (
                        value(row, c)
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
        </tbody>
      </table>
      {showEmpty && !rows.length && (
        <p className="muted text-sm mt-4">Aucun mouvement retenu pour ce modèle.</p>
      )}
    </div>
  );
}
