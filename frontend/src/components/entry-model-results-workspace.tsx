'use client';
import { useEffect, useState } from 'react';
import Link from 'next/link';
import {
  BookOpen,
  Search,
  Download,
  Archive,
  CheckCircle2,
  PanelRightClose,
  PanelRightOpen,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { BankDialog } from '@/components/bank-dialogs';
import { LoadState } from '@/components/ui/load-state';
import { ConversionSteps } from '@/components/conversion-steps';
import { EntryResultTable } from '@/components/entry-result-table';
import { StatementPdfPanel } from '@/components/statement-pdf-panel';
import { EntrySourceSummary } from '@/components/entry-source-summary';
import { useAll, useApi } from '@/services/use-api';
import { API_URL, apiError, request } from '@/services/api';
import {
  money,
  type StatementTransaction,
  type StatementDetail,
} from '@/services/statement-exports';
import type {
  EntryModelDossier,
  EntryModelPreview,
  EntryModelRun,
  EntryOutputTemplate,
} from '@/services/entry-models';
import { saveReadyExportBlob } from '@/lib/save-export';
import { downloadExcel } from '@/lib/download-excel';
import { hasUnsavedChanges, useUnsavedChanges } from '@/lib/use-unsaved-changes';
import { useAuth } from '@/components/auth-context';
import '@/styles/entry-models.css';

const accountCategories = [
  ['all', 'Toutes les catégories'],
  ['invalid', 'Mouvements à corriger'],
  ['vat', 'TVA'],
  ['commission', 'Commissions'],
  ['fee', 'Frais'],
  ['transfer', 'Virements'],
  ['cheque', 'Chèques'],
  ['mixed', 'Commission et TVA regroupées'],
  ['other', 'Autres'],
] as const;

function summarizeIssues(issues: { code: string; message: string }[]) {
  const counts = new Map<string, number>();
  for (const issue of issues) {
    const message = issue.message.replace(/^Ligne \d+ : /, '');
    counts.set(message, (counts.get(message) ?? 0) + 1);
  }
  return [...counts].map(([message, count]) =>
    count > 1 ? `${count} mouvements · ${message}` : message,
  );
}
type Source = {
  id: string;
  bankName: string;
  sourceFileName: string | null;
  statementNumber: string | null;
  clientId: string | null;
};
const normalizeLabel = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toUpperCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
export function EntryModelResultsWorkspace({
  clientId,
  initialSource,
  initialExtract,
  initialAbbreviation,
  guided = false,
  onNavigate,
  initialPreview,
  initialModelId = '',
  initialOutputTemplateId = '',
  initialRun,
}: {
  clientId: string;
  initialSource: string;
  initialExtract: string;
  initialFormat: string;
  initialAbbreviation: string;
  guided?: boolean;
  onNavigate?: (step: 1 | 2) => void;
  initialPreview?: EntryModelPreview;
  initialModelId?: string;
  initialOutputTemplateId?: string;
  initialRun?: EntryModelRun;
}) {
  const { can } = useAuth();
  const sources = useAll<Source>(`/client-workspace/${clientId}/statements`);
  const models = useApi<EntryModelDossier>(`/accounting/entry-models/${clientId}`);
  const formats = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [sourceId, setSourceId] = useState(initialSource);
  const [extractId, setExtractId] = useState(initialExtract);
  const formatId = '00000000-0000-0000-0000-000000000000';
  const [selectedModelId, setSelectedModelId] = useState(initialModelId);
  const [outputTemplateId, setOutputTemplateId] = useState(initialOutputTemplateId);
  const [abbreviation, setAbbreviation] = useState(initialAbbreviation);
  const detail = useApi<StatementDetail>(sourceId ? `/statement-imports/${sourceId}` : null);
  const [decisions, setDecisions] = useState<Record<string, string | null>>(
    initialRun?.decisions || {},
  );
  const [preview, setPreview] = useState<EntryModelPreview | null>(initialPreview || null);
  const [previewKey, setPreviewKey] = useState(
    initialPreview
      ? JSON.stringify({
          clientId,
          statementId: initialSource,
          exportTemplateId: '00000000-0000-0000-0000-000000000000',
          abbreviation: initialAbbreviation,
          ...(initialModelId ? { modelId: initialModelId } : {}),
          ...(initialOutputTemplateId ? { outputTemplateId: initialOutputTemplateId } : {}),
          ...(initialRun?.keywordOverrides
            ? { keywordOverrides: initialRun.keywordOverrides }
            : {}),
          ...(initialExtract ? { extractId: initialExtract } : {}),
          decisions: initialRun?.decisions || {},
          ...(initialRun?.balancedScope
            ? {
                balancedScope: initialRun.balancedScope,
                counterAccounts: initialRun.counterAccounts || {},
                ...(initialRun.confirmDefaultVat ? { confirmDefaultVat: true } : {}),
                ...(initialRun.feeVatRates ? { feeVatRates: initialRun.feeVatRates } : {}),
                ...(initialRun.defaultCommissionVatRate
                  ? { defaultCommissionVatRate: initialRun.defaultCommissionVatRate }
                  : {}),
                ...(initialRun.lineEdits ? { lineEdits: initialRun.lineEdits } : {}),
              }
            : {}),
        })
      : '',
  );
  const legacyFeesScope = initialRun?.balancedScope === 'fees';
  const [balancedScope, setBalancedScope] = useState<'' | 'all'>(
    initialRun?.balancedScope || (guided && !initialRun) ? 'all' : '',
  );
  const [counterAccounts, setCounterAccounts] = useState(initialRun?.counterAccounts || {});
  const [accountCategory, setAccountCategory] = useState('commission');
  const [batchAccount, setBatchAccount] = useState('');
  const [confirmDefaultVat, setConfirmDefaultVat] = useState(!!initialRun?.confirmDefaultVat);
  const [feeVatRates, setFeeVatRates] = useState(initialRun?.feeVatRates);
  const [defaultCommissionVatRate, setDefaultCommissionVatRate] = useState(
    initialRun?.defaultCommissionVatRate || 0,
  );
  const [lineEdits, setLineEdits] = useState(legacyFeesScope ? undefined : initialRun?.lineEdits);
  const [reviewed, setReviewed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState(
    legacyFeesScope && Object.keys(initialRun?.lineEdits ?? {}).length > 0
      ? 'Le relevé complet a une nouvelle numérotation. Vérifiez à nouveau les lignes modifiées dans l’ancien extrait.'
      : '',
  );
  const [downloadFormat, setDownloadFormat] = useState<'txt' | 'excel'>('txt');
  const [confirmDownload, setConfirmDownload] = useState<'excel' | 'file' | 'archive' | null>(null);
  const [phase, setPhase] = useState<4 | 5 | 6>(4);
  const [pdfCollapsed, setPdfCollapsed] = useState(false);
  const [keywordDraft, setKeywordDraft] = useState<string | null>(
    initialRun?.keywordOverrides?.join('\n') ?? null,
  );
  const eligible = (models.data?.models || []).filter(
    (m) => m.active && m.bankAccountId === detail.data?.bankAccountId,
  );
  const abbreviations = [...new Set(eligible.map((m) => m.abbreviation))].sort();
  const matchingModels = eligible.filter((m) => m.abbreviation === abbreviation);
  const processingModel =
    matchingModels.find((m) => m.id === selectedModelId) ||
    (matchingModels.length === 1 ? matchingModels[0] : undefined);
  const runModelId = processingModel?.id || '';
  const displayFormat = formats.data?.find(
    (t) => t.id === (outputTemplateId || processingModel?.outputTemplateId),
  );
  const expectsSageColumns =
    !!balancedScope || !!displayFormat?.output.columns.some((column) => column.field === 'PIECE');
  const outdatedColumns =
    !!preview &&
    ((expectsSageColumns && !preview.output?.columns.some((column) => column.field === 'PIECE')) ||
      (!!balancedScope &&
        (!preview.commissionOnlyVat ||
          preview.rows.some(
            (row) => row.accountCategory === 'commission' && !('linkedVatId' in row),
          ))));
  const accountRows =
    preview?.rows.filter(
      (row) =>
        accountCategory === 'all' ||
        (accountCategory === 'invalid'
          ? row.status === 'INVALID'
          : row.accountCategory === accountCategory),
    ) || [];
  const validBatchAccount = /^[A-Za-z0-9]{1,32}$/.test(batchAccount.trim());
  const issueSummary = summarizeIssues(preview?.errors || []);
  const activeWords =
    keywordDraft === null
      ? processingModel?.keywords || []
      : [
          ...new Set(
            keywordDraft
              .split(/\r?\n/)
              .map((w) => w.trim())
              .filter(Boolean),
          ),
        ];
  const sourceLabels =
    preview?.rows.map((r) => r.description) ||
    detail.data?.statement.transactions.map((t) => t.description || '') ||
    [];
  function changeWords(value: string | null) {
    setKeywordDraft(value);
    setDecisions({});
    setReviewed(false);
    setNotice('');
    if (guided) setPhase(5);
  }
  const input: EntryModelRun = {
    clientId,
    statementId: sourceId,
    exportTemplateId: formatId,
    abbreviation,
    ...(runModelId ? { modelId: runModelId } : {}),
    ...(outputTemplateId ? { outputTemplateId } : {}),
    ...(keywordDraft !== null ? { keywordOverrides: activeWords } : {}),
    ...(extractId ? { extractId } : {}),
    decisions,
    ...(balancedScope
      ? {
          balancedScope,
          counterAccounts,
          ...(confirmDefaultVat ? { confirmDefaultVat: true } : {}),
          ...(feeVatRates ? { feeVatRates } : {}),
          ...(defaultCommissionVatRate ? { defaultCommissionVatRate } : {}),
          ...(lineEdits ? { lineEdits } : {}),
        }
      : {}),
  };
  const inputKey = JSON.stringify(input);
  const stale = inputKey !== previewKey || outdatedColumns;
  const lines = preview?.resultRows || [];
  const vatLines = new Map(
    lines.filter((row) => row.accountCode === '34552100').map((row) => [row.transactionId, row]),
  );
  const debit = lines.reduce((sum, row) => sum + Math.round(Number(row.debitHt) * 100), 0);
  const credit = lines.reduce((sum, row) => sum + Math.round(Number(row.creditHt) * 100), 0);
  const unbalanced =
    debit !== credit || preview?.errors.some((issue) => issue.code === 'UNBALANCED');
  const incomplete = preview?.rows.some((row) => row.status === 'INVALID');
  const canFinalize =
    !!preview &&
    (preview.canExport ||
      (can('documents.write') &&
        preview.errors.length > 0 &&
        preview.errors.every((issue) => issue.code === 'SOURCE_NOT_VALIDATED')));
  const canForce =
    !!preview &&
    !preview.canExport &&
    lines.length > 0 &&
    !preview.errors.some((issue) =>
      ['TXT_FORMAT', 'BANK_ACCOUNT', 'BANK_JOURNAL', 'CURRENCY', 'LINE_EDIT'].includes(issue.code),
    );
  const loading = sources.loading || models.loading || detail.loading || formats.loading;
  const failure = error || sources.error || models.error || detail.error || formats.error;
  const canPreview =
    !!processingModel &&
    !!detail.data?.bankAccountId &&
    detail.data.clientId === clientId &&
    sources.data?.some((s) => s.id === sourceId) &&
    abbreviations.includes(abbreviation) &&
    (!!balancedScope || activeWords.length > 0);
  const needsFullPreview = balancedScope === 'all' && !previewKey.includes('"balancedScope":"all"');
  useEffect(() => {
    if (
      (!guided && !needsFullPreview) ||
      !canPreview ||
      loading ||
      busy ||
      (preview && !outdatedColumns && !needsFullPreview)
    )
      return;
    const controller = new AbortController();
    request<EntryModelPreview>('/accounting/entry-models/preview', {
      method: 'POST',
      body: inputKey,
      signal: controller.signal,
    })
      .then((result) => {
        if (!controller.signal.aborted) {
          setPreview(result);
          setPreviewKey(inputKey);
        }
      })
      .catch((reason: Error) => {
        if (!controller.signal.aborted) setError(reason.message);
      });
    return () => controller.abort();
  }, [guided, canPreview, loading, busy, preview, inputKey, outdatedColumns, needsFullPreview]);
  function reset() {
    setKeywordDraft(null);
    setDecisions({});
    setReviewed(false);
    setNotice('');
  }
  async function inspect() {
    if (!canPreview || busy) return;
    if (guided && hasUnsavedChanges()) {
      setError('Enregistrez les modifications du document source avant de recalculer le résultat.');
      return;
    }
    setBusy(true);
    setError('');
    setReviewed(false);
    setNotice('');
    try {
      setPreview(
        await request<EntryModelPreview>('/accounting/entry-models/preview', {
          method: 'POST',
          body: JSON.stringify(input),
        }),
      );
      setPreviewKey(inputKey);
      if (guided && phase === 6) setPhase(5);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function download(confirmed = false, archiveOnly = false, forced = false) {
    if (busy) return;
    if (!preview || !processingModel) {
      setError('Choisissez une écriture et affichez son résultat avant le téléchargement.');
      return;
    }
    if (stale) {
      setError('Enregistrez les modifications avant de finaliser les écritures.');
      return;
    }
    if (!reviewed && !confirmed) return;
    if (guided && hasUnsavedChanges()) {
      setError(
        'Enregistrez les modifications du document source et recalculez le résultat avant le téléchargement.',
      );
      return;
    }
    const needsValidation = preview.errors.some((issue) => issue.code === 'SOURCE_NOT_VALIDATED');
    const blocking = preview.errors.filter((issue) => issue.code !== 'SOURCE_NOT_VALIDATED');
    if (!forced && (blocking.length || (!preview.canExport && !needsValidation))) {
      setError(
        summarizeIssues(blocking).join(' ') || 'Recalculez le résultat avant le téléchargement.',
      );
      return;
    }
    if (!forced && needsValidation && !can('documents.write')) {
      setError(
        'Le document doit être validé par un utilisateur autorisé à modifier les documents avant le téléchargement.',
      );
      return;
    }
    setBusy(true);
    setError('');
    try {
      const extension =
        preview.output?.fileExtension || processingModel.output?.fileExtension || 'txt';
      const name = `ecritures_${abbreviation}_${sourceId.replaceAll('-', '').slice(0, 8)}${forced ? '_A_VERIFIER' : ''}.${extension}`;
      let exportPreview = preview;
      if (needsValidation && !forced) {
        const validated = await request<StatementDetail>(
          `/statement-imports/${sourceId}/validate`,
          {
            method: 'POST',
            body: JSON.stringify({ version: preview.sourceVersion }),
          },
        );
        detail.reload();
        if (validated.status !== 'VALIDATED' || validated.errors.length) {
          throw new Error(
            validated.errors.map((issue) => issue.message).join(' ') ||
              'Le document source doit encore être vérifié.',
          );
        }
        exportPreview = await request<EntryModelPreview>('/accounting/entry-models/preview', {
          method: 'POST',
          body: JSON.stringify(input),
        });
        setPreview(exportPreview);
        setPreviewKey(inputKey);
        if (
          exportPreview.settingsVersion !== preview.settingsVersion ||
          JSON.stringify(exportPreview.output) !== JSON.stringify(preview.output) ||
          JSON.stringify(exportPreview.resultRows) !== JSON.stringify(preview.resultRows)
        ) {
          throw new Error(
            'Le modèle ou le résultat a changé pendant la validation. Vérifiez le nouvel aperçu, puis téléchargez le fichier.',
          );
        }
        if (!exportPreview.canExport) {
          throw new Error(
            exportPreview.errors.map((issue) => issue.message).join(' ') ||
              'Le résultat doit encore être vérifié.',
          );
        }
      }
      const response = await fetch(`${API_URL}/accounting/entry-models/export`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-Alias-Request': '1' },
        body: JSON.stringify({
          ...input,
          previewFingerprint: exportPreview.fingerprint,
          confirmed: true,
          forceUnverifiedDownload: forced,
        }),
      });
      if (!response.ok) throw await apiError(response);
      const file = await response.blob();
      if (!archiveOnly) await saveReadyExportBlob(file, name, extension);
      setReviewed(false);
      if (guided) setPhase(6);
      setNotice(
        forced
          ? archiveOnly
            ? 'Fichier archivé avec la mention À vérifier.'
            : 'Téléchargement exceptionnel confirmé. Fichier archivé avec la mention À vérifier.'
          : archiveOnly
            ? 'Écritures archivées dans l’espace client avec le modèle et les paramètres utilisés.'
            : 'Résultat enregistré dans l’espace client et téléchargé.',
      );
      try {
        setPreview(
          await request<EntryModelPreview>('/accounting/entry-models/preview', {
            method: 'POST',
            body: JSON.stringify(input),
          }),
        );
        setPreviewKey(inputKey);
      } catch {
        setPreview(null);
      }
    } catch (e) {
      if ((e as Error).name !== 'AbortError') setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  function downloadExcelResult() {
    if (!preview?.output || !preview.resultRows) return;
    const columns = preview.output.columns.filter((column) => column.export);
    const values: Record<string, keyof (typeof preview.resultRows)[number]> = {
      ACCOUNT: 'accountCode',
      TITLE: 'title',
      DESCRIPTION: 'description',
      BANK: 'bank',
      NET: 'net',
      DEBIT_HT: 'debitHt',
      CREDIT_HT: 'creditHt',
      GROSS: 'gross',
      TAX: 'tax',
      RATE: 'rate',
      DATE: 'date',
      REFERENCE: 'reference',
      ABBREVIATION: 'abbreviation',
      CURRENCY: 'currency',
    };
    downloadExcel(
      `ecritures_${abbreviation}_${sourceId.replaceAll('-', '').slice(0, 8)}${preview.canExport ? '' : '_A_VERIFIER'}.xlsx`,
      columns.map((column) => column.label),
      preview.resultRows.map((row) =>
        columns.map((column) => {
          if (column.field === 'JOURNAL') return row.journal ?? row.abbreviation;
          if (column.field === 'PIECE') return row.pieceNumber ?? row.reference;
          if (column.field === 'DATE' && row.date) {
            const [year, month, day] = row.date.slice(0, 10).split('-');
            const parts: Record<string, string> = {
              yyyy: year,
              yy: year.slice(-2),
              MM: month,
              dd: day,
            };
            return (column.format || preview.output?.dateFormat || 'dd/MM/yyyy').replace(
              /yyyy|yy|MM|dd/g,
              (part) => parts[part],
            );
          }
          if (['DEBIT_HT', 'CREDIT_HT', 'NET', 'GROSS', 'TAX', 'RATE'].includes(column.field)) {
            const amount = row[values[column.field]];
            if (
              amount != null &&
              Number.isFinite(Number(amount)) &&
              String(amount)
                .replace(/[^0-9]/g, '')
                .replace(/^0+/, '').length <= 15
            )
              return Number(amount);
          }
          return row[values[column.field]] ?? '';
        }),
      ),
      columns.flatMap((column, index) =>
        ['DEBIT_HT', 'CREDIT_HT'].includes(column.field) ? [index] : [],
      ),
    );
    setNotice('Le fichier Excel des écritures a été téléchargé.');
    setError('');
  }
  async function confirmVerification() {
    if (busy || !preview || !processingModel || !can('documents.write')) return;
    if (stale || hasUnsavedChanges()) {
      setError('Enregistrez les changements et actualisez le résultat avant de confirmer.');
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const validated = await request<StatementDetail>(`/statement-imports/${sourceId}/validate`, {
        method: 'POST',
        body: JSON.stringify({ version: preview.sourceVersion }),
      });
      detail.reload();
      if (validated.status !== 'VALIDATED' || validated.errors.length) {
        throw new Error(
          validated.errors.map((issue) => issue.message).join(' ') ||
            'Le document source doit encore être vérifié.',
        );
      }
      const refreshed = await request<EntryModelPreview>('/accounting/entry-models/preview', {
        method: 'POST',
        body: JSON.stringify(input),
      });
      setPreview(refreshed);
      setPreviewKey(inputKey);
      setReviewed(true);
      setNotice('Vérification confirmée. Le résultat a été actualisé.');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  const retained = preview?.rows.filter((r) => r.status === 'MATCHED').length || 0;
  const ambiguous =
    preview?.rows.filter((r) => r.status === 'AMBIGUOUS' || r.status === 'INVALID').length || 0;
  const excluded =
    preview?.rows.filter((r) => r.status === 'UNMATCHED' || r.status === 'EXCLUDED').length || 0;
  const resultRows = preview?.rows.filter((r) => r.status === 'MATCHED') || [];
  const resultBank =
    sources.data?.find((s) => s.id === preview?.statementId)?.bankName ||
    (detail.data?.id === preview?.statementId ? detail.data?.statement.bankName : '') ||
    '—';
  const [movement, setMovement] = useState<StatementTransaction | null>(null);
  const sourceMovement = detail.data?.statement.transactions.find((row) => row.id === movement?.id);
  const movementChanged = !!movement && JSON.stringify(movement) !== JSON.stringify(sourceMovement);
  useUnsavedChanges(movementChanged);

  async function saveValues(event: React.FormEvent) {
    event.preventDefault();
    const source = detail.data;
    if (
      !source ||
      !movement ||
      !sourceMovement ||
      !movementChanged ||
      busy ||
      extractId ||
      !can('documents.write')
    )
      return;
    const amount = (value: StatementTransaction['debit']) =>
      String(value ?? '')
        .trim()
        .replace(',', '.');
    const debitValue = amount(movement.debit);
    const creditValue = amount(movement.credit);
    const valid = (value: string) => /^\d{1,15}(?:\.\d{1,2})?$/.test(value) && Number(value) > 0;
    if (
      !(
        (valid(debitValue) && (!creditValue || Number(creditValue) === 0)) ||
        (valid(creditValue) && (!debitValue || Number(debitValue) === 0))
      )
    ) {
      setError(
        'Renseignez un seul montant positif, au débit ou au crédit, avec deux décimales au maximum.',
      );
      return;
    }
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const saved = await request<StatementDetail>(
        `/statement-imports/${source.id}?version=${source.version}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            ...source.statement,
            transactions: source.statement.transactions.map((row) =>
              row.id === movement.id
                ? {
                    ...movement,
                    debit: valid(debitValue) ? debitValue : null,
                    credit: valid(creditValue) ? creditValue : null,
                    amount: null,
                    direction: null,
                  }
                : row,
            ),
          }),
        },
      );
      setMovement(saved.statement.transactions.find((row) => row.id === movement.id) || null);
      setReviewed(false);
      setPreview(null);
      setPreviewKey('');
      detail.reload();
      const result = await request<EntryModelPreview>('/accounting/entry-models/preview', {
        method: 'POST',
        body: inputKey,
      });
      setPreview(result);
      setPreviewKey(inputKey);
      setNotice('Valeurs enregistrées. Les écritures ont été recalculées.');
      if (guided) setPhase(5);
    } catch (cause) {
      setError((cause as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="entry-model-results-workspace">
      {confirmDownload && (
        <BankDialog
          title={
            confirmDownload === 'archive' ? 'Confirmer l’archivage' : 'Confirmer le téléchargement'
          }
          onClose={() => setConfirmDownload(null)}
        >
          <p>
            {unbalanced ? 'L’écriture est non équilibrée.' : 'La vérification est incomplète.'} Le
            fichier contiendra les lignes affichées et portera la mention « À vérifier ».
            {confirmDownload !== 'excel' && ' Il sera archivé dans l’espace client.'}
          </p>
          <div className="mt-4 flex flex-wrap justify-end gap-2">
            <Button variant="outline" onClick={() => setConfirmDownload(null)}>
              Annuler
            </Button>
            <Button
              permission="documents.export"
              disabled={busy || stale}
              onClick={() => {
                setConfirmDownload(null);
                if (confirmDownload === 'excel') downloadExcelResult();
                else void download(true, confirmDownload === 'archive', true);
              }}
            >
              {confirmDownload === 'archive' ? 'Confirmer et archiver' : 'Confirmer et télécharger'}
            </Button>
          </div>
        </BankDialog>
      )}
      {guided && detail.data && (
        <EntrySourceSummary
          detail={detail.data}
          clientId={clientId}
          modelName={
            processingModel
              ? `${processingModel.abbreviation} · ${processingModel.name}`
              : undefined
          }
          accountCode={processingModel?.accountCode}
        />
      )}
      {guided && !preview && !failure && (
        <p role="status" className="muted text-sm">
          Chargement du résultat…
        </p>
      )}
      {guided && (
        <section className="ui-card">
          <ConversionSteps
            current={phase === 6 ? 4 : 3}
            accountingMode
            extractMode={detail.data?.statement.conversionType === 'EXTRACT'}
            disabled={busy}
            onNavigate={(step) => {
              if (step <= 2) onNavigate?.(step as 1 | 2);
              else setPhase(step === 4 ? 6 : 4);
            }}
          />
          <div className="entry-model-toolbar mt-4">
            <h2 className="font-semibold">
              {phase === 4
                ? 'Vérifiez le résultat de l’écriture'
                : phase === 5
                  ? 'Paramétrez les mouvements retenus'
                  : 'Téléchargez vos écritures comptables'}
            </h2>
            <span className="ui-tag">
              {abbreviation} · {displayFormat?.name || processingModel?.name}
            </span>
          </div>
        </section>
      )}
      <section className="ui-card">
        <label>
          Format Sage 100 équilibré
          <select
            className="ui-input mt-2"
            value={balancedScope}
            disabled={busy}
            onChange={(event) => {
              setBalancedScope(event.target.value === 'all' ? 'all' : '');
              setReviewed(false);
            }}
          >
            <option value="">Format d’écriture sélectionné</option>
            <option value="all">Tous les mouvements du relevé ou de l’extrait</option>
          </select>
        </label>
        {balancedScope && (
          <div className="mt-3 space-y-3">
            <fieldset className="rounded-xl border bg-muted/20 p-4" disabled={busy}>
              <legend className="px-1 text-sm font-semibold">Application de la TVA</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {[
                  {
                    all: true,
                    label: 'Toutes les commissions',
                    description: 'Reprendre la TVA vérifiée du relevé au compte 34552100.',
                  },
                  {
                    all: false,
                    label: 'Par ligne',
                    description: 'Choisir la TVA ou le taux directement sur chaque commission.',
                  },
                ].map((mode) => (
                  <label
                    key={mode.label}
                    className={`flex cursor-pointer items-start gap-2 rounded-lg border p-3 text-sm ${confirmDefaultVat === mode.all ? 'border-primary bg-card' : 'bg-card'}`}
                  >
                    <input
                      type="radio"
                      name="commission-vat-mode"
                      className="mt-1 accent-primary"
                      checked={confirmDefaultVat === mode.all}
                      onChange={() => {
                        setConfirmDefaultVat(mode.all);
                        if (!mode.all) setDefaultCommissionVatRate(0);
                        setReviewed(false);
                      }}
                    />
                    <span>
                      <strong className="block">{mode.label}</strong>
                      <span className="muted mt-1 block">{mode.description}</span>
                    </span>
                  </label>
                ))}
              </div>
              {(confirmDefaultVat || defaultCommissionVatRate > 0) && (
                <div className="mt-3">
                  <label className="block max-w-md text-sm">
                    Taux de TVA inclus dans le total des commissions
                    <select
                      className="ui-input mt-1"
                      value={defaultCommissionVatRate}
                      disabled={busy}
                      onChange={(event) => {
                        setDefaultCommissionVatRate(Number(event.target.value));
                        setReviewed(false);
                      }}
                    >
                      <option value={0}>Aucun calcul automatique</option>
                      {[7, 10, 14, 20].map((rate) => (
                        <option key={rate} value={rate}>
                          {rate} % · TVA incluse
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              )}
            </fieldset>
            <div id="entry-accounts" className="space-y-3 rounded-xl border p-4">
              <div className="flex flex-wrap items-end gap-3">
                <label className="min-w-40 flex-1 text-sm">
                  Catégorie
                  <select
                    className="ui-input mt-1"
                    value={accountCategory}
                    onChange={(event) => {
                      setAccountCategory(event.target.value);
                      setBatchAccount('');
                    }}
                  >
                    {accountCategories.map(([key, label]) => (
                      <option key={key} value={key}>
                        {label} (
                        {preview?.rows.filter(
                          (row) =>
                            key === 'all' ||
                            (key === 'invalid'
                              ? row.status === 'INVALID'
                              : row.accountCategory === key),
                        ).length ?? 0}
                        )
                      </option>
                    ))}
                  </select>
                </label>
                <label className="min-w-36 flex-1 text-sm">
                  Compte à appliquer au lot
                  <input
                    className="ui-input mt-1"
                    value={batchAccount}
                    maxLength={32}
                    onChange={(event) => setBatchAccount(event.target.value)}
                  />
                </label>
                <Button
                  type="button"
                  variant="outline"
                  disabled={busy || !accountRows.length || !validBatchAccount}
                  onClick={() => {
                    setCounterAccounts((current) => ({
                      ...current,
                      ...Object.fromEntries(
                        accountRows.map((row) => [row.transactionId, batchAccount.trim()]),
                      ),
                    }));
                    setReviewed(false);
                  }}
                >
                  Appliquer à {accountRows.length} ligne(s)
                </Button>
              </div>
              {accountRows.length ? (
                <div className="max-h-80 overflow-auto rounded-lg border">
                  <table className="w-full text-sm">
                    <thead className="sticky top-0 bg-card text-left">
                      <tr>
                        <th className="p-2">Ligne</th>
                        <th className="p-2">Libellé</th>
                        <th className="p-2 text-right">Montant</th>
                        <th className="p-2">Compte</th>
                        <th className="p-2">TVA de la commission</th>
                      </tr>
                    </thead>
                    <tbody>
                      {accountRows.map((row) => {
                        const selectedRate =
                          feeVatRates?.[row.transactionId] ?? defaultCommissionVatRate;
                        const vatLine = row.linkedVatId ? vatLines.get(row.linkedVatId) : undefined;
                        const appliedVat =
                          vatLine?.sequence === row.sequence
                            ? Number(vatLine.debitHt) + Number(vatLine.creditHt)
                            : null;
                        return (
                          <tr key={row.transactionId} className="border-t">
                            <td className="p-2">{row.sequence}</td>
                            <td className="p-2">{row.description}</td>
                            <td className="p-2 text-right whitespace-nowrap">
                              {row.total == null ? 'À vérifier' : money(row.total)}
                            </td>
                            <td className="w-36 p-2">
                              <input
                                aria-label={`Compte de contrepartie ligne ${row.sequence}`}
                                className="ui-input min-w-24"
                                maxLength={32}
                                disabled={busy}
                                value={
                                  counterAccounts[row.transactionId] ?? row.counterAccount ?? ''
                                }
                                onChange={(event) => {
                                  setCounterAccounts((current) => ({
                                    ...current,
                                    [row.transactionId]: event.target.value,
                                  }));
                                  setReviewed(false);
                                }}
                              />
                            </td>
                            <td className="p-2">
                              {(confirmDefaultVat || selectedRate > 0) && row.linkedVatId ? (
                                <span className="whitespace-nowrap">
                                  {counterAccounts[row.linkedVatId] &&
                                  counterAccounts[row.linkedVatId] !== '34552100'
                                    ? `Compte ${counterAccounts[row.linkedVatId]}`
                                    : 'TVA du relevé'}{' '}
                                  · {money(row.linkedVatAmount ?? null)}
                                </span>
                              ) : row.linkedVatId ? (
                                <label className="flex min-w-36 items-center gap-2">
                                  <input
                                    type="checkbox"
                                    disabled={busy || confirmDefaultVat}
                                    checked={
                                      (counterAccounts[row.linkedVatId] ??
                                        (confirmDefaultVat ? '34552100' : '')) === '34552100'
                                    }
                                    onChange={(event) => {
                                      const id = row.linkedVatId!;
                                      const checked = event.target.checked;
                                      setCounterAccounts((current) => {
                                        const next = { ...current };
                                        if (checked) next[id] = '34552100';
                                        else delete next[id];
                                        return next;
                                      });
                                      setReviewed(false);
                                    }}
                                  />
                                  TVA du relevé · {money(row.linkedVatAmount ?? null)}
                                </label>
                              ) : null}
                              {(row.accountCategory === 'commission' ||
                                row.accountCategory === 'mixed') &&
                                (!confirmDefaultVat || !!row.linkedVatId) && (
                                  <select
                                    className="ui-input mt-1 min-w-32"
                                    aria-label={`Taux de TVA inclus ligne ${row.sequence}`}
                                    disabled={busy}
                                    value={feeVatRates?.[row.transactionId] ?? ''}
                                    onChange={(event) => {
                                      const value = event.target.value;
                                      setFeeVatRates((current) => {
                                        const next = { ...current };
                                        if (value === '') delete next[row.transactionId];
                                        else next[row.transactionId] = Number(value);
                                        return next;
                                      });
                                      setReviewed(false);
                                    }}
                                  >
                                    <option value="">
                                      {defaultCommissionVatRate
                                        ? `Taux global · ${defaultCommissionVatRate} %`
                                        : 'Selon le relevé'}
                                    </option>
                                    <option value={0}>Selon le relevé uniquement</option>
                                    {[7, 10, 14, 20].map((rate) => (
                                      <option key={rate} value={rate}>
                                        TTC · {rate} %
                                      </option>
                                    ))}
                                  </select>
                                )}
                              {appliedVat !== null &&
                                appliedVat !== Number(row.linkedVatAmount) && (
                                  <div className="mt-1 whitespace-nowrap text-sm font-medium">
                                    TVA recalculée · {money(appliedVat)}
                                  </div>
                                )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="muted text-sm">Aucun mouvement dans cette catégorie.</p>
              )}
            </div>
            <Button
              id="entry-save-parameters"
              disabled={busy || loading || !canPreview}
              onClick={() => void inspect()}
            >
              Enregistrer et recalculer les écritures
            </Button>
          </div>
        )}
      </section>
      {detail.data && can('documents.write') && (
        <details className="ui-card">
          <summary className="cursor-pointer font-semibold">Modifier les valeurs du relevé</summary>
          {extractId ? (
            <Link
              className="mt-3 inline-block underline"
              href={`/conversions?statementId=${sourceId}&extractId=${extractId}&step=result`}
            >
              Ouvrir l’éditeur de l’extrait
            </Link>
          ) : (
            <form className="mt-3 space-y-3" onSubmit={(event) => void saveValues(event)}>
              <label className="block text-sm">
                Mouvement
                <select
                  className="ui-input mt-1"
                  value={movement?.id || ''}
                  disabled={busy}
                  onChange={(event) => {
                    if (
                      movementChanged &&
                      !window.confirm('Abandonner les corrections non enregistrées ?')
                    )
                      return;
                    setMovement(
                      detail.data?.statement.transactions.find(
                        (row) => row.id === event.target.value,
                      ) || null,
                    );
                  }}
                >
                  <option value="">Choisir un mouvement…</option>
                  {detail.data.statement.transactions.map((row) => (
                    <option key={row.id} value={row.id}>
                      {row.sequence} · {row.description}
                    </option>
                  ))}
                </select>
              </label>
              {movement && (
                <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2">
                  <label className="text-sm">
                    Date
                    <input
                      className="ui-input mt-1"
                      required
                      type="date"
                      value={movement.date || ''}
                      onChange={(event) => setMovement({ ...movement, date: event.target.value })}
                    />
                  </label>
                  <label className="text-sm">
                    Libellé
                    <input
                      className="ui-input mt-1"
                      required
                      value={movement.description || ''}
                      onChange={(event) =>
                        setMovement({ ...movement, description: event.target.value })
                      }
                    />
                  </label>
                  <label className="text-sm">
                    Débit
                    <input
                      className="ui-input mt-1"
                      inputMode="decimal"
                      value={movement.debit ?? ''}
                      onChange={(event) => setMovement({ ...movement, debit: event.target.value })}
                    />
                  </label>
                  <label className="text-sm">
                    Crédit
                    <input
                      className="ui-input mt-1"
                      inputMode="decimal"
                      value={movement.credit ?? ''}
                      onChange={(event) => setMovement({ ...movement, credit: event.target.value })}
                    />
                  </label>
                </fieldset>
              )}
              <Button type="submit" disabled={!movementChanged || busy}>
                Enregistrer les valeurs
              </Button>
            </form>
          )}
        </details>
      )}
      {!sources.loading && !sources.error && sources.data?.length === 0 && (
        <section className="ui-card entry-model-empty">
          <BookOpen size={28} />
          <h2>Aucun document classé pour ce client</h2>
          <p>
            Importez un relevé ou un extrait et associez-le au client et à son compte bancaire pour
            appliquer ses modèles d’écritures.
          </p>
          <Button permission="documents.write" asChild>
            <Link href={`/conversions?import=1&type=accounting&clientId=${clientId}`}>
              Importer un document bancaire
            </Link>
          </Button>
        </section>
      )}
      <div
        className={
          guided
            ? `entry-conversion-review-layout${pdfCollapsed ? ' is-pdf-collapsed' : ''}`
            : undefined
        }
      >
        <div className="entry-conversion-results">
          <details
            className={`ui-card${guided ? ' entry-model-guided-controls' : ''}`}
            open={!guided || undefined}
          >
            <summary className="font-semibold">Filtrer les mouvements</summary>
            {guided && (
              <div className="entry-filter-selection">
                <label className="conversion-template-label">
                  Abréviation
                  <select
                    aria-label="Abréviation enregistrée"
                    className="ui-input mt-2"
                    value={abbreviation}
                    disabled={busy || loading}
                    onChange={(e) => {
                      setAbbreviation(e.target.value);
                      setSelectedModelId('');
                      reset();
                    }}
                  >
                    <option value="">Choisir une abréviation…</option>
                    {abbreviations.map((item) => (
                      <option key={item} value={item}>
                        {item}
                      </option>
                    ))}
                  </select>
                </label>
                {matchingModels.length > 1 && (
                  <label className="conversion-template-label">
                    Modèle
                    <select
                      aria-label="Modèle de l’abréviation"
                      className="ui-input mt-2"
                      value={selectedModelId}
                      disabled={busy || loading}
                      onChange={(e) => {
                        setSelectedModelId(e.target.value);
                        reset();
                      }}
                    >
                      <option value="">Choisir un modèle…</option>
                      {matchingModels.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name} · {item.accountCode} · TVA {item.taxRate} %
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                <p className="muted text-sm">
                  Les mots et la TVA du modèle sont appliqués au document.
                </p>
              </div>
            )}
            {processingModel && (
              <div className="entry-model-keyword-controls">
                <div className="entry-model-keywords" aria-label="Mots du modèle">
                  {activeWords.map((word) => (
                    <span key={word}>
                      {word} ·{' '}
                      {
                        sourceLabels.filter((label) =>
                          ` ${normalizeLabel(label)} `.includes(` ${normalizeLabel(word)} `),
                        ).length
                      }
                    </span>
                  ))}
                </div>
                <details className="entry-filter-advanced">
                  <summary>Ajuster les mots pour cet export</summary>
                  <label className="conversion-template-label">
                    Mots du libellé
                    <textarea
                      aria-label={`Mots du libellé pour ${abbreviation}`}
                      className="ui-input mt-2"
                      rows={2}
                      disabled={busy}
                      value={keywordDraft ?? processingModel.keywords.join('\n')}
                      onChange={(e) => changeWords(e.target.value)}
                    />
                  </label>
                  <p className="muted text-sm">
                    Une expression par ligne. Le modèle enregistré reste inchangé.
                  </p>
                  {keywordDraft !== null && (
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={busy}
                      onClick={() => changeWords(null)}
                    >
                      Rétablir les mots
                    </Button>
                  )}
                </details>
              </div>
            )}
            {!guided && (
              <>
                <div className="entry-model-fields">
                  <label>
                    Relevé ou extrait PDF
                    <select
                      aria-label="Document source"
                      className="ui-input mt-2"
                      disabled={busy || sources.loading}
                      value={sourceId}
                      onChange={(e) => {
                        setSourceId(e.target.value);
                        setExtractId('');
                        setAbbreviation('');
                        reset();
                      }}
                    >
                      <option value="">Choisir le document…</option>
                      {sources.data?.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.sourceFileName || s.statementNumber || s.bankName} · {s.bankName}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Modèle d’exportation des écritures
                    <select
                      className="ui-input mt-2"
                      disabled={busy || formats.loading}
                      value={outputTemplateId}
                      onChange={(e) => {
                        setOutputTemplateId(e.target.value);
                        setReviewed(false);
                      }}
                    >
                      <option value="">Format enregistré dans la règle</option>
                      {formats.data
                        ?.filter((t) => t.active || t.id === outputTemplateId)
                        .map((t) => (
                          <option key={t.id} value={t.id} disabled={!t.active}>
                            {t.name}
                          </option>
                        ))}
                    </select>
                  </label>
                  <label>
                    Règle comptable du client
                    <select
                      className="ui-input mt-2"
                      aria-label="Règle comptable du client"
                      value={processingModel?.id || ''}
                      disabled={busy || models.loading}
                      onChange={(e) => {
                        setSelectedModelId(e.target.value);
                        setAbbreviation(
                          eligible.find((m) => m.id === e.target.value)?.abbreviation || '',
                        );
                        reset();
                      }}
                    >
                      <option value="">Choisir une règle du client…</option>
                      {eligible.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.abbreviation} · {t.name} · TVA {t.taxRate} %
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Périmètre
                    <select
                      className="ui-input mt-2"
                      value={extractId}
                      disabled={busy || !detail.data}
                      onChange={(e) => {
                        setExtractId(e.target.value);
                        reset();
                      }}
                    >
                      <option value="">Toutes les opérations du document</option>
                      {detail.data?.statement.extracts?.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.filterText || 'Extrait sauvegardé'} · {x.transactions.length}{' '}
                          mouvements
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Abréviation
                    <select
                      className="ui-input mt-2"
                      aria-label="Abréviation du traitement"
                      disabled={busy || !formatId || !detail.data}
                      value={abbreviation}
                      onChange={(e) => {
                        setAbbreviation(e.target.value);
                        setSelectedModelId('');
                        reset();
                      }}
                    >
                      <option value="">Choisir une abréviation…</option>
                      {abbreviations.map((a) => (
                        <option key={a} value={a}>
                          {a} · {eligible.filter((m) => m.abbreviation === a).length} modèle(s)
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              </>
            )}
            {sourceId && detail.data && !detail.data.bankAccountId && (
              <p className="conversion-alert warning mt-4">
                Associez le document à un compte du client avant le traitement.{' '}
                <Link className="underline" href={`/conversions?statementId=${sourceId}`}>
                  Ouvrir et classer le document
                </Link>
              </p>
            )}
            {formatId &&
              detail.data?.bankAccountId &&
              !models.loading &&
              !models.error &&
              !eligible.length && (
                <p className="conversion-alert warning mt-4">
                  Aucune règle active pour ce compte.{' '}
                  <Link
                    className="underline"
                    href={`/conversions?type=accounting&step=model&statementId=${sourceId}${outputTemplateId ? '&outputTemplateId=' + outputTemplateId : ''}${extractId ? '&extractId=' + extractId : ''}`}
                  >
                    Configurer la règle de ce document
                  </Link>
                </p>
              )}
            <div className="entry-model-actions mt-5">
              <Button disabled={!canPreview || busy || loading} onClick={() => void inspect()}>
                <Search size={16} /> {busy ? 'Traitement…' : 'Appliquer le filtre'}
              </Button>
            </div>
          </details>
          <LoadState loading={loading} error="" />
          {failure && (
            <div className="conversion-alert error" role="alert">
              {failure}
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => {
                  setError('');
                  sources.reload();
                  models.reload();
                  formats.reload();
                  detail.reload();
                }}
              >
                Réessayer
              </Button>
            </div>
          )}
          {notice && (
            <p className="ui-card" role="status">
              {notice}
            </p>
          )}
          {preview && (
            <>
              <div className="entry-model-summary">
                <div className="ui-card">
                  <span>Mouvements retenus</span>
                  <strong>{retained}</strong>
                </div>
                <div className="ui-card">
                  <span>À vérifier</span>
                  <strong>{ambiguous}</strong>
                </div>
                <div className="ui-card">
                  <span>Non retenus</span>
                  <strong>{excluded}</strong>
                </div>
              </div>
              <section
                className="ui-card entry-model-review"
                aria-labelledby="entry-model-output-title"
              >
                <div className="entry-model-toolbar">
                  <div>
                    <h2 id="entry-model-output-title" className="font-semibold">
                      Résultat des écritures
                    </h2>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="ui-tag">
                      {balancedScope
                        ? `${resultRows.length} / ${preview.rows.length} mouvements comptabilisés`
                        : `${resultRows.length} mouvement(s)`}
                    </span>
                  </div>
                </div>
                {issueSummary.length > 0 && (
                  <details className="my-3 rounded-lg border px-3 py-2 text-sm">
                    <summary className="cursor-pointer font-medium">
                      Vérifications à effectuer · {issueSummary.length}
                    </summary>
                    <ul className="mt-2 list-disc space-y-1 pl-5">
                      {issueSummary.map((issue) => (
                        <li key={issue}>{issue}</li>
                      ))}
                    </ul>
                  </details>
                )}
                {preview.output && preview.resultRows ? (
                  <EntryResultTable
                    output={preview.output}
                    rows={preview.resultRows}
                    showEmpty={false}
                    editing={
                      balancedScope && can('documents.write')
                        ? {
                            drafts: lineEdits || {},
                            busy: busy || loading || !canPreview,
                            change: (index, draft) => {
                              setLineEdits((current) => ({ ...current, [index]: draft }));
                              setReviewed(false);
                            },
                            reset: () => {
                              setLineEdits(undefined);
                              setReviewed(false);
                            },
                            save: () => void inspect(),
                          }
                        : undefined
                    }
                  />
                ) : (
                  <div className="entry-model-table-wrap">
                    <table
                      className="entry-model-results-table entry-model-output-table"
                      aria-label="Résultat du modèle après TVA"
                    >
                      <thead>
                        <tr>
                          <th scope="col">N° compte</th>
                          <th scope="col">Intitulé</th>
                          <th scope="col">Libellé</th>
                          <th scope="col">Banque</th>
                          <th scope="col" className="amount">
                            Montant
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {resultRows.map((row) => {
                          const model = row.candidates.find((c) => c.id === row.modelId);
                          return (
                            <tr key={row.transactionId}>
                              <td data-label="N° compte" className="entry-model-output-account">
                                {model?.accountCode || '—'}
                              </td>
                              <td data-label="Intitulé" className="font-medium">
                                {model?.name || '—'}
                              </td>
                              <td data-label="Libellé">{row.description || '—'}</td>
                              <td data-label="Banque">{resultBank}</td>
                              <td data-label="Montant HT" className="amount">
                                {money(row.net)}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
                {balancedScope && preview.rows.some((row) => row.status === 'INVALID') && (
                  <a
                    href="#entry-accounts"
                    className="mt-3 inline-block underline"
                    onClick={() => {
                      setAccountCategory('invalid');
                      setBatchAccount('');
                    }}
                  >
                    Corriger les mouvements dans le tableau des comptes
                  </a>
                )}
                {!(preview.resultRows?.length || resultRows.length) &&
                  (!balancedScope || !preview.rows.some((row) => row.status === 'INVALID')) && (
                    <p className="muted text-sm" role="status">
                      Aucun mouvement retenu pour ce modèle.
                    </p>
                  )}
                <section className="entry-completion-bar" aria-label="Finaliser le traitement">
                  {balancedScope && (
                    <div className="entry-completion-summary" role="status">
                      <p>
                        Débit {money((debit / 100).toFixed(2))} · Crédit{' '}
                        {money((credit / 100).toFixed(2))}
                      </p>
                      <strong>
                        {stale
                          ? 'Modifications à enregistrer'
                          : !lines.length
                            ? 'Aucune pièce à vérifier'
                            : unbalanced
                              ? 'Écriture non équilibrée'
                              : incomplete
                                ? 'Pièces affichées équilibrées · résultat incomplet'
                                : 'Pièces équilibrées'}
                      </strong>
                    </div>
                  )}
                  {balancedScope && stale && (
                    <a href="#entry-save-parameters" className="inline-block underline">
                      Aller à l’enregistrement des modifications
                    </a>
                  )}
                  <div className="entry-completion-actions">
                    <Button
                      permission="documents.export"
                      variant="outline"
                      size="sm"
                      disabled={busy || loading || stale || (!canForce && !canFinalize)}
                      onClick={() =>
                        canForce ? setConfirmDownload('archive') : void download(true, true)
                      }
                    >
                      <Archive size={16} /> Terminer et archiver
                    </Button>
                    {can('documents.export') && (
                      <div
                        className="flex flex-wrap items-center gap-2"
                        role="group"
                        aria-label="Téléchargement"
                      >
                        <select
                          aria-label="Format du téléchargement"
                          className="ui-input"
                          style={{ width: 'auto' }}
                          value={downloadFormat}
                          disabled={busy}
                          onChange={(event) =>
                            setDownloadFormat(event.target.value === 'excel' ? 'excel' : 'txt')
                          }
                        >
                          <option value="txt">
                            {preview.output?.fileExtension?.toUpperCase() || 'TXT'}
                          </option>
                          <option value="excel">Excel</option>
                        </select>
                        <Button
                          permission="documents.export"
                          disabled={
                            busy ||
                            loading ||
                            stale ||
                            (downloadFormat === 'excel'
                              ? !lines.length ||
                                !preview.output?.columns.some((column) => column.export)
                              : !canForce && !canFinalize)
                          }
                          size="sm"
                          onClick={() => {
                            if (downloadFormat === 'excel') {
                              if (preview.canExport) downloadExcelResult();
                              else setConfirmDownload('excel');
                            } else if (canForce) setConfirmDownload('file');
                            else void download(true, false);
                          }}
                        >
                          <Download size={16} /> Télécharger
                        </Button>
                      </div>
                    )}
                    {detail.data?.status === 'VALIDATED' ? (
                      <span
                        className="status-positive inline-flex items-center gap-2"
                        role="status"
                      >
                        <CheckCircle2 size={16} /> Vérification confirmée
                      </span>
                    ) : (
                      <Button
                        permission="documents.write"
                        variant="outline"
                        size="sm"
                        disabled={busy || loading || stale || !preview || !can('documents.write')}
                        onClick={() => void confirmVerification()}
                      >
                        {busy ? 'Confirmation…' : 'Confirmer la vérification'}
                      </Button>
                    )}
                  </div>
                </section>
              </section>
              {preview.lines.length > 0 && (
                <details className="ui-card entry-model-result-details">
                  <summary>Écritures comptables détaillées · {preview.lines.length} lignes</summary>
                  <div className="entry-model-table-wrap mt-4">
                    <table className="entry-model-results-table">
                      <thead>
                        <tr>
                          <th>Journal / pièce</th>
                          <th>Compte</th>
                          <th>Libellé</th>
                          <th className="amount">Débit</th>
                          <th className="amount">Crédit</th>
                        </tr>
                      </thead>
                      <tbody>
                        {preview.lines.map((line, index) => (
                          <tr key={index}>
                            <td data-label="Journal / pièce">
                              {line.journal} · {line.pieceNumber}
                            </td>
                            <td data-label="Compte">{line.accountCode}</td>
                            <td data-label="Libellé">{line.description}</td>
                            <td data-label="Débit" className="amount">
                              {money(line.debit)}
                            </td>
                            <td data-label="Crédit" className="amount">
                              {money(line.credit)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </details>
              )}
            </>
          )}
        </div>
        {guided && detail.data && (
          <div className="entry-conversion-document">
            <div className="entry-document-heading">
              <h2 className="font-semibold">Document source</h2>
              <Button
                variant="ghost"
                size="sm"
                aria-label={pdfCollapsed ? 'Afficher le PDF' : 'Réduire le PDF'}
                aria-expanded={!pdfCollapsed}
                aria-controls="entry-source-pdf"
                onClick={() => setPdfCollapsed(!pdfCollapsed)}
              >
                {pdfCollapsed ? <PanelRightOpen size={16} /> : <PanelRightClose size={16} />}
                {pdfCollapsed ? 'Afficher' : 'Réduire'}
              </Button>
            </div>
            <div id="entry-source-pdf" hidden={pdfCollapsed}>
              <StatementPdfPanel
                id={detail.data.id}
                version={detail.data.version}
                hasRows={detail.data.statement.transactions.length > 0}
                extractId={extractId || undefined}
              />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
