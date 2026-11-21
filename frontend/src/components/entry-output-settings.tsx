'use client';
import { ArrowUp, ArrowDown, Plus, Trash2 } from 'lucide-react';
import { Button } from './ui/button';
import {
  defaultEntryColumnFormat,
  entryDateFormats,
  entryFields,
  entryNumericFields,
  type EntryModelOutput,
} from '@/services/entry-models';

const numberFormats = ['0.00', '0.000', '0.##'];
const numberCultures = [
  ['fr-FR', 'Français (123,45)'],
  ['en-US', 'Anglais (123.45)'],
];
function formatsFor(field: string) {
  if (field === 'DATE') return ['', ...entryDateFormats];
  if (entryNumericFields.has(field))
    return numberCultures.flatMap(([culture]) =>
      numberFormats.map((format) => `number:${culture}:${format}`),
    );
  return [''];
}
function formatLabel(format: string) {
  if (format.startsWith('number:')) {
    const [, culture, pattern] = format.split(':');
    const cultureName = numberCultures.find(([value]) => value === culture)?.[1] || culture;
    return `${cultureName} · ${pattern}`;
  }
  return format || 'Par défaut';
}

export function EntryOutputSettings({
  value,
  onChange,
  disabled,
}: {
  value: EntryModelOutput;
  onChange: (value: EntryModelOutput) => void;
  disabled: boolean;
}) {
  function move(index: number, direction: number) {
    const columns = [...value.columns];
    const next = index + direction;
    [columns[index], columns[next]] = [columns[next], columns[index]];
    onChange({ ...value, columns });
  }

  return (
    <fieldset disabled={disabled} className="entry-output-settings">
      <legend>2. Réglez le fichier · 3. Choisissez les colonnes</legend>
      <section className="entry-output-file-settings" aria-labelledby="entry-output-file-title">
        <h3 id="entry-output-file-title">2. Réglez le fichier</h3>
        <p className="muted text-sm">
          Choisissez le type de fichier et reprenez les réglages attendus par votre logiciel
          comptable.
        </p>
        <div className="entry-model-fields">
          <label>
            Type de fichier
            <select
              aria-label="Type de fichier"
              className="ui-input mt-2"
              value={value.fileExtension || 'txt'}
              onChange={(e) =>
                onChange({ ...value, fileExtension: e.target.value as 'txt' | 'csv' | 'tsv' })
              }
            >
              <option value="txt">Fichier texte (.txt)</option>
              <option value="csv">Fichier CSV (.csv)</option>
              <option value="tsv">Fichier TSV (.tsv)</option>
            </select>
          </label>
          <label>
            Séparateur du fichier
            <select
              aria-label="Séparateur du fichier"
              className="ui-input mt-2"
              value={value.delimiter}
              onChange={(e) => onChange({ ...value, delimiter: e.target.value })}
            >
              {[
                [';', 'Point-virgule'],
                ['\t', 'Tabulation'],
                ['|', 'Barre verticale'],
                [',', 'Virgule'],
              ].map(([v, label]) => (
                <option key={v} value={v}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Encodage
            <select
              aria-label="Encodage"
              className="ui-input mt-2"
              value={value.encoding}
              onChange={(e) => onChange({ ...value, encoding: e.target.value })}
            >
              <option value="utf-8">UTF-8</option>
              <option value="windows-1252">Windows-1252</option>
            </select>
          </label>
          <label className="entry-model-check">
            <input
              type="checkbox"
              checked={value.includeHeader}
              onChange={(e) => onChange({ ...value, includeHeader: e.target.checked })}
            />{' '}
            Inclure les en-têtes dans le fichier
          </label>
        </div>
      </section>

      <section className="entry-output-columns" aria-labelledby="entry-output-columns-title">
        <div className="entry-output-section-heading">
          <div>
            <h3 id="entry-output-columns-title">3. Choisissez les colonnes</h3>
            <p className="muted text-sm">
              L’ordre définit l’affichage des résultats et celui des colonnes exportées.
            </p>
          </div>
          <span className="entry-output-column-count">{value.columns.length} colonnes</span>
        </div>
        {value.columns.map((column, index) => (
          <div className="entry-output-column" key={index}>
            <div className="entry-output-column-heading">
              <strong>Colonne {index + 1}</strong>
              <div className="entry-output-column-actions">
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  aria-label={`Monter colonne ${index + 1}`}
                  disabled={disabled || index === 0}
                  onClick={() => move(index, -1)}
                >
                  <ArrowUp size={15} />
                </Button>
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  aria-label={`Descendre colonne ${index + 1}`}
                  disabled={disabled || index === value.columns.length - 1}
                  onClick={() => move(index, 1)}
                >
                  <ArrowDown size={15} />
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  disabled={disabled || value.columns.length === 1}
                  onClick={() =>
                    onChange({ ...value, columns: value.columns.filter((_, i) => i !== index) })
                  }
                >
                  <Trash2 size={15} /> Retirer la colonne {index + 1}
                </Button>
              </div>
            </div>
            <div className="entry-output-column-fields">
              <label>
                Libellé {index + 1}
                <input
                  className="ui-input mt-2"
                  aria-label={`En-tête colonne ${index + 1}`}
                  value={column.label}
                  required
                  maxLength={80}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index ? { ...c, label: e.target.value } : c,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Source {index + 1}
                <select
                  className="ui-input mt-2"
                  aria-label={`Source ${index + 1}`}
                  value={column.field}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index
                          ? {
                              ...c,
                              field: e.target.value,
                              label: entryFields[e.target.value],
                              format: defaultEntryColumnFormat(
                                e.target.value,
                                value.numberCulture,
                                value.dateFormat,
                              ),
                            }
                          : c,
                      ),
                    })
                  }
                >
                  {Object.entries(entryFields)
                    .filter(
                      ([field]) =>
                        field === column.field || !value.columns.some((c) => c.field === field),
                    )
                    .map(([field, label]) => (
                      <option key={field} value={field}>
                        {label}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                Format {index + 1}
                <select
                  className="ui-input mt-2"
                  aria-label={`Format colonne ${index + 1}`}
                  value={
                    column.format ??
                    defaultEntryColumnFormat(column.field, value.numberCulture, value.dateFormat)
                  }
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index ? { ...c, format: e.target.value || null } : c,
                      ),
                    })
                  }
                >
                  {formatsFor(column.field).map((format) => (
                    <option key={format || 'default'} value={format}>
                      {formatLabel(format)}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="entry-output-column-options">
              <label className="entry-model-check">
                <input
                  type="checkbox"
                  checked={column.required ?? false}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index ? { ...c, required: e.target.checked } : c,
                      ),
                    })
                  }
                />{' '}
                Colonne {index + 1} obligatoire
              </label>
              <label className="entry-model-check">
                <input
                  type="checkbox"
                  checked={column.display}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index ? { ...c, display: e.target.checked } : c,
                      ),
                    })
                  }
                />{' '}
                Afficher
              </label>
              <label className="entry-model-check">
                <input
                  type="checkbox"
                  checked={column.export}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      columns: value.columns.map((c, i) =>
                        i === index ? { ...c, export: e.target.checked } : c,
                      ),
                    })
                  }
                />{' '}
                Exporter
              </label>
            </div>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          disabled={disabled || value.columns.length >= Object.keys(entryFields).length}
          onClick={() => {
            const field = Object.keys(entryFields).find(
              (candidate) => !value.columns.some((column) => column.field === candidate),
            );
            if (field) {
              onChange({
                ...value,
                columns: [
                  ...value.columns,
                  {
                    field,
                    label: entryFields[field],
                    display: true,
                    export: true,
                    format: defaultEntryColumnFormat(field, value.numberCulture, value.dateFormat),
                    required: false,
                  },
                ],
              });
            }
          }}
        >
          <Plus size={15} /> Ajouter une colonne
        </Button>
      </section>
      {!value.columns.some((column) => column.display) ||
      !value.columns.some((column) => column.export) ? (
        <p role="alert" className="conversion-alert warning">
          Conservez au moins une colonne pour l’affichage et une colonne pour l’export.
        </p>
      ) : null}
    </fieldset>
  );
}
