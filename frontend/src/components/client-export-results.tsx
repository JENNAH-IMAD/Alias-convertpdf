'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Download, FileSpreadsheet, Search } from 'lucide-react';
import { documentBlob, type Page } from '@/services/api';
import { useApi } from '@/services/use-api';
import { chooseExportDestination, saveExportBlob } from '@/lib/save-export';
import { Button } from './ui/button';
import { Pagination } from './ui/pagination';
import { LoadState } from './ui/load-state';

type Result = {
  id: string;
  statementId: string;
  kind: string;
  sourceName: string;
  model: string;
  fileName: string;
  encoding: string;
  delimiter: string;
  hasFile: boolean;
  createdAt: string;
};
type Model = { id: string; kind: string; label: string };

export function ClientExportResults({ clientId }: { clientId: string }) {
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [model, setModel] = useState('');
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const models = useApi<Model[]>('/client-workspace/model-filters');
  const params = new URLSearchParams({ page: String(page), pageSize: '20' });
  if (clientId) params.set('clientId', clientId);
  if (term) params.set('search', term);
  const [kind, id] = model.split(':');
  if (id) params.set(kind === 'entry-output' ? 'entryOutputTemplateId' : 'exportTemplateId', id);
  const results = useApi<Page<Result>>(`/client-workspace/results?${params}`);

  async function download(result: Result, excel: boolean) {
    setBusy(result.id);
    setError('');
    const url =
      result.kind === 'entry'
        ? `/accounting/entry-models/archives/${result.id}/file`
        : `/statement-imports/${result.statementId}/exports/archives/${result.id}/file`;
    try {
      if (excel) {
        const [xlsx, blob] = await Promise.all([import('xlsx'), documentBlob(url)]);
        const text = new TextDecoder(result.encoding || 'utf-8').decode(await blob.arrayBuffer());
        const aligned = result.delimiter === '  ';
        const workbook = aligned
          ? xlsx.utils.book_new()
          : xlsx.read(text, {
              type: 'string',
              raw: true,
              FS: result.delimiter || ',',
            });
        if (aligned) {
          const rows = text
            .trimEnd()
            .split(/\r?\n/)
            .map((line) => {
              const match =
                /^(\S+) {2,}(\S+) {2,}(\S+) {2,}(\S+)( {2,})(.*?)( {2,})(\S+) {2,}(\S+)$/.exec(
                  line,
                );
              if (!match)
                throw new Error(
                  'Une ligne du TXT aligné ne contient pas les sept champs attendus.',
                );
              return [match[1], match[2], match[3], match[4], match[6], match[8], match[9]];
            });
          xlsx.utils.book_append_sheet(
            workbook,
            xlsx.utils.aoa_to_sheet([
              ['Journal', 'Date', 'Compte', 'N° pièce', 'Libellé', 'Débit', 'Crédit'],
              ...rows,
            ]),
            'Écritures',
          );
        }
        xlsx.writeFile(workbook, result.fileName.replace(/\.[^.]+$/, '') + '.xlsx', {
          compression: true,
        });
      } else {
        const extension = result.fileName.split('.').pop() || 'txt';
        const destination = await chooseExportDestination(result.fileName, extension);
        await saveExportBlob(await documentBlob(url), destination);
      }
    } catch (cause) {
      if ((cause as Error).name !== 'AbortError') setError((cause as Error).message);
    } finally {
      setBusy('');
    }
  }

  return (
    <section className="ui-card space-y-4" aria-labelledby="client-export-results">
      <div>
        <h2 id="client-export-results" className="text-lg font-semibold">
          Résultats et écritures comptables
        </h2>
        <p className="muted text-sm">Aperçus et exports archivés, du plus récent au plus ancien.</p>
      </div>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          setTerm(search.trim());
          setPage(1);
        }}
      >
        <label className="min-w-56 flex-1">
          Rechercher un résultat
          <input
            className="ui-input mt-2"
            value={search}
            maxLength={150}
            placeholder="Nom du relevé, du modèle ou du fichier"
            onChange={(event) => setSearch(event.target.value)}
          />
        </label>
        <label className="min-w-56 flex-1">
          Modèle d’exportation du résultat
          <select
            className="ui-input mt-2"
            value={model}
            onChange={(event) => {
              setModel(event.target.value);
              setPage(1);
            }}
          >
            <option value="">Tous les modèles</option>
            {models.data?.map((item) => (
              <option key={`${item.kind}:${item.id}`} value={`${item.kind}:${item.id}`}>
                {item.label}
              </option>
            ))}
          </select>
        </label>
        <Button type="submit" variant="outline">
          <Search size={16} /> Rechercher
        </Button>
      </form>
      {error && (
        <p role="alert" className="conversion-alert error">
          {error}
        </p>
      )}
      <LoadState loading={results.loading} error={results.error || models.error} />
      {results.data?.total === 0 && (
        <p className="muted text-sm">Aucun fichier archivé pour cette sélection.</p>
      )}
      <ul className="space-y-2">
        {results.data?.items.map((result) => (
          <li
            key={`${result.kind}:${result.statementId}:${result.id}`}
            className="flex flex-wrap items-center justify-between gap-3 rounded-xl border p-4"
          >
            <div className="min-w-0">
              <span className="ui-tag">
                {result.kind === 'preview'
                  ? 'Écriture à vérifier'
                  : result.kind === 'entry'
                    ? 'Écriture comptable'
                    : 'Export bancaire'}
              </span>
              <p className="mt-2 font-semibold break-words">
                {result.sourceName || result.fileName}
              </p>
              <p className="muted text-sm">
                {result.model} · {new Date(result.createdAt).toLocaleString('fr-FR')}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button asChild size="sm" variant="outline">
                <Link
                  href={`/conversions?statementId=${result.statementId}${result.kind === 'preview' && result.id !== '00000000-0000-0000-0000-000000000000' ? '&extractId=' + result.id : ''}`}
                >
                  {result.kind === 'preview' ? 'Ouvrir l’aperçu' : 'Ouvrir le relevé'}
                </Link>
              </Button>
              {result.hasFile && (
                <>
                  <Button
                    permission="documents.export"
                    size="sm"
                    variant="outline"
                    disabled={!!busy}
                    onClick={() => void download(result, false)}
                  >
                    <Download size={15} /> Fichier
                  </Button>
                  <Button
                    permission="documents.export"
                    size="sm"
                    variant="outline"
                    disabled={!!busy}
                    onClick={() => void download(result, true)}
                  >
                    <FileSpreadsheet size={15} /> Excel
                  </Button>
                </>
              )}
            </div>
          </li>
        ))}
      </ul>
      {results.data && (
        <Pagination
          label="Pagination des résultats"
          itemLabel="résultat(s)"
          total={results.data.total}
          page={page}
          loading={results.loading}
          onChange={setPage}
        />
      )}
    </section>
  );
}
