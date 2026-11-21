'use client';
import { useState } from 'react';
import Link from 'next/link';
import { Columns3, Copy, Download, FileOutput, Pencil, Plus, Trash2 } from 'lucide-react';
import { request, documentBlob } from '@/services/api';
import { useApi } from '@/services/use-api';
import {
  defaultEntryColumnFormat,
  defaultEntryOutput,
  type EntryOutputTemplate,
} from '@/services/entry-models';
import { Button } from './ui/button';
import { BankDialog } from './bank-dialogs';
import { LoadState } from './ui/load-state';
import { EntryOutputSettings } from './entry-output-settings';
import { chooseExportDestination, saveExportBlob } from '@/lib/save-export';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';
import { PageHeader } from './page-header';
import { TemplateExcelButton } from './template-excel-button';
import '@/styles/entry-models.css';

export function EntryExportTemplates() {
  const templates = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [editing, setEditing] = useState<EntryOutputTemplate | null>(null);
  const [removing, setRemoving] = useState<EntryOutputTemplate | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const empty = (): EntryOutputTemplate => ({
    id: '',
    name: '',
    description: '',
    version: 0,
    active: true,
    output: defaultEntryOutput(),
  });
  async function save(value: EntryOutputTemplate) {
    setBusy(true);
    setError('');
    try {
      await request(`/accounting/entry-output-templates${value.id ? '/' + value.id : ''}`, {
        method: value.id ? 'PUT' : 'POST',
        body: JSON.stringify(value),
      });
      setEditing(null);
      setNotice('Modèle d’exportation des écritures enregistré.');
      templates.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function remove() {
    if (!removing) return;
    setBusy(true);
    setError('');
    try {
      await request(
        `/accounting/entry-output-templates/${removing.id}?version=${removing.version}`,
        { method: 'DELETE' },
      );
      setRemoving(null);
      setNotice('Modèle d’exportation supprimé. Les résultats archivés sont conservés.');
      templates.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function structure(item: EntryOutputTemplate) {
    setBusy(true);
    setError('');
    try {
      const extension = item.output.fileExtension || 'txt';
      const destination = await chooseExportDestination(`modele_ecritures.${extension}`, extension);
      await saveExportBlob(
        await documentBlob(`/accounting/entry-output-templates/${item.id}/file`),
        destination,
      );
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className="model-catalog-content"
      aria-label="Modèles d’exportation des écritures comptables"
    >
      <PageHeader
        icon={FileOutput}
        eyebrow="Formats de fichiers"
        title="Modèles d’exportation"
        description="Gérez les formats CSV et Sage, leurs colonnes et leurs réglages."
        actions={
          <Button
            permission="templates.write"
            onClick={() => {
              setError('');
              setEditing(empty());
            }}
          >
            <Plus size={17} /> Ajouter un modèle
          </Button>
        }
      />
      <LoadState loading={templates.loading} error={templates.error} />
      <p className="ui-card">
        Pour exporter les pièces équilibrées au format Sage 100, importez le PDF, puis choisissez «
        Format Sage 100 équilibré » à la vérification. Le TXT contient le journal, la date, le
        compte, la pièce, le libellé, le débit et le crédit en colonnes alignées, sans en-tête.
      </p>
      {error && !editing && !removing && (
        <p className="conversion-alert error" role="alert">
          {error}
        </p>
      )}
      {notice && (
        <p className="ui-card" role="status">
          {notice}
        </p>
      )}
      <div className="catalog-grid template-grid">
        {templates.data?.map((item) => (
          <article key={item.id} className="ui-card template-card">
            <div className="template-card-top">
              <span className="template-format-icon">
                <FileOutput size={23} />
              </span>
              <span className={item.active ? 'status-positive' : 'ui-tag'}>
                {item.active ? 'Actif' : 'Inactif'}
              </span>
            </div>
            <h2 className="text-lg font-semibold">{item.name}</h2>
            <p className="template-format-meta">
              .{item.output.fileExtension || 'txt'} · {item.output.encoding} ·{' '}
              {item.output.fileExtension === 'txt' &&
              item.output.delimiter === '\t' &&
              !item.output.includeHeader &&
              item.output.columns
                .filter((column) => column.export)
                .map((column) => column.field)
                .join('|') === 'JOURNAL|DATE|ACCOUNT|PIECE|DESCRIPTION|DEBIT_HT|CREDIT_HT'
                ? 'Colonnes alignées'
                : item.output.delimiter === '\t'
                  ? 'Tabulation'
                  : item.output.delimiter}{' '}
              · {item.output.includeHeader ? 'Avec en-têtes' : 'Sans en-têtes'}
            </p>
            <p className="template-description">
              {item.description || 'Format d’exportation des écritures comptables.'}
            </p>
            <div className="template-field-summary">
              <strong>
                <Columns3 size={16} />
                {item.output.columns.filter((c) => c.export).length} colonnes exportées
              </strong>
              <div>
                {item.output.columns
                  .filter((c) => c.export)
                  .map((c) => (
                    <span key={c.field}>{c.label}</span>
                  ))}
              </div>
            </div>
            {item.active && (
              <Link
                className="template-use-link"
                href={`/conversions?import=1&type=accounting&outputTemplateId=${item.id}`}
              >
                Importer un PDF avec ce modèle
              </Link>
            )}
            <div className="catalog-card-actions">
              <TemplateExcelButton
                name={item.name}
                columns={item.output.columns.filter((c) => c.export).map((c) => c.label)}
              />
              <Button
                permission="templates.write"
                variant="outline"
                onClick={() => {
                  setError('');
                  setEditing(item);
                }}
              >
                <Pencil size={15} /> Modifier
              </Button>
              <Button
                permission="templates.write"
                variant="outline"
                onClick={() => {
                  setError('');
                  setEditing({ ...item, id: '', version: 0, name: item.name + ' — copie' });
                }}
              >
                <Copy size={15} /> Dupliquer
              </Button>
              <Button
                permission="documents.export"
                variant="outline"
                size="icon"
                aria-label={`Télécharger le modèle ${item.name}`}
                title="Télécharger le modèle"
                disabled={busy}
                onClick={() => void structure(item)}
              >
                <Download size={16} />
              </Button>
              <Button
                permission="templates.write"
                variant="outline"
                size="icon"
                aria-label={`Supprimer ${item.name}`}
                title="Supprimer le modèle"
                className="danger-button"
                onClick={() => {
                  setError('');
                  setRemoving(item);
                }}
              >
                <Trash2 size={16} />
              </Button>
            </div>
          </article>
        ))}
      </div>
      {!templates.loading && !templates.error && !templates.data?.length && (
        <section className="ui-card template-empty-state">
          <FileOutput size={24} />
          <h2>Aucun modèle enregistré</h2>
          <p>Créez un format d’écritures à partir des colonnes comptables proposées.</p>
        </section>
      )}
      {editing && (
        <EntryExportTemplateEditor
          key={editing.id || 'new'}
          initial={editing}
          busy={busy}
          error={error}
          onClose={() => setEditing(null)}
          onSave={save}
        />
      )}
      {removing && (
        <BankDialog
          title="Supprimer le format d’écritures"
          busy={busy}
          onClose={() => setRemoving(null)}
        >
          <p>
            Supprimer « {removing.name} » ? Un format utilisé par une écriture doit d’abord être
            remplacé dans celle-ci.
          </p>
          {error && (
            <p className="conversion-alert error mt-4" role="alert">
              {error}
            </p>
          )}
          <div className="entry-model-actions mt-5">
            <Button variant="outline" disabled={busy} onClick={() => setRemoving(null)}>
              Annuler
            </Button>
            <Button variant="danger" disabled={busy} onClick={() => void remove()}>
              Supprimer le format
            </Button>
          </div>
        </BankDialog>
      )}
    </section>
  );
}

function EntryExportTemplateEditor({
  initial,
  busy,
  error,
  onClose,
  onSave,
}: {
  initial: EntryOutputTemplate;
  busy: boolean;
  error: string;
  onClose: () => void;
  onSave: (value: EntryOutputTemplate) => Promise<void>;
}) {
  const normalizedInitial = {
    ...initial,
    output: {
      ...initial.output,
      columns: initial.output.columns.map((column) => ({
        ...column,
        required: column.required ?? false,
        format:
          column.format ??
          defaultEntryColumnFormat(
            column.field,
            initial.output.numberCulture,
            initial.output.dateFormat,
          ),
      })),
    },
  };
  const [draft, setDraft] = useState<EntryOutputTemplate>(() => normalizedInitial);
  useUnsavedChanges(JSON.stringify(draft) !== JSON.stringify(normalizedInitial));
  return (
    <BankDialog
      wide
      title={initial.id ? 'Modifier le modèle' : 'Ajouter un modèle'}
      busy={busy}
      onClose={onClose}
      className="entry-output-template-dialog"
    >
      <form
        className="entry-model-form entry-output-template-form"
        onSubmit={(e) => {
          e.preventDefault();
          void onSave(draft);
        }}
      >
        <div className="entry-output-template-guide">
          <strong>Votre modèle en 3 étapes</strong>
          <p>1. Nommez le format · 2. Réglez le fichier · 3. Choisissez les colonnes</p>
        </div>
        <fieldset disabled={busy} className="entry-output-template-identity">
          <legend>1. Nommez le format</legend>
          <div className="entry-output-template-identity-fields">
            <label>
              Nom du modèle
              <input
                className="ui-input mt-2"
                required
                maxLength={100}
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              />
            </label>
            <label>
              Description
              <textarea
                className="ui-input mt-2"
                maxLength={1000}
                value={draft.description}
                onChange={(e) => setDraft({ ...draft, description: e.target.value })}
              />
            </label>
            <label className="entry-model-check">
              <input
                type="checkbox"
                checked={draft.active}
                disabled={busy}
                onChange={(e) => setDraft({ ...draft, active: e.target.checked })}
              />{' '}
              Modèle actif
            </label>
          </div>
        </fieldset>
        <EntryOutputSettings
          value={draft.output}
          disabled={busy}
          onChange={(output) => setDraft({ ...draft, output })}
        />
        {error && (
          <p className="conversion-alert error" role="alert">
            {error}
          </p>
        )}
        <div className="entry-model-actions">
          <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
            Annuler
          </Button>
          <Button
            type="submit"
            disabled={
              busy ||
              !draft.output.columns.some((c) => c.display) ||
              !draft.output.columns.some((c) => c.export)
            }
          >
            Enregistrer
          </Button>
        </div>
      </form>
    </BankDialog>
  );
}
