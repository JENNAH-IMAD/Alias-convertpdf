'use client';

import { useState } from 'react';
import Link from 'next/link';
import { ArrowRight, BookOpen } from 'lucide-react';
import { Button } from './ui/button';
import { ConversionSteps } from './conversion-steps';
import { EntryModelEditor, newEntryRule } from './client-entry-models';
import { useAll, useApi } from '@/services/use-api';
import { request, type Account, type Bank, type Client } from '@/services/api';
import type {
  EntryModel,
  EntryModelDossier,
  EntryModelPreview,
  EntryOutputTemplate,
} from '@/services/entry-models';
import type { StatementDetail } from '@/services/statement-exports';

export type EntryFlowSelection = {
  clientId: string;
  bankAccountId: string;
  exportTemplateId: string;
  abbreviation: string;
  modelId: string;
  outputTemplateId?: string;
};
const fold = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]/gi, '')
    .toUpperCase();

export function AccountingEntryModelFlow({
  detail,
  initial,
  onReview,
  onImport,
  onReload,
  extractId,
  embedded = false,
  onBack,
}: {
  detail: StatementDetail;
  initial: EntryFlowSelection;
  onReview: (selection: EntryFlowSelection, preview: EntryModelPreview) => void;
  onImport: () => void;
  onReload: () => void;
  extractId?: string;
  embedded?: boolean;
  onBack?: () => void;
}) {
  const clients = useAll<Client>('/clients');
  const accounts = useAll<Account>('/bank-accounts');
  const banks = useAll<Bank>('/banks');
  const formats = useApi<EntryOutputTemplate[]>('/accounting/entry-output-templates');
  const [standaloneFormat, setFormatChoice] = useState(initial.outputTemplateId || '');
  const formatChoice = embedded ? initial.outputTemplateId || '' : standaloneFormat;
  const format = formats.data?.find((item) => item.id === formatChoice && item.active);
  const [draftRule, setDraftRule] = useState<EntryModel | null>(null);
  const [clientId, setClientId] = useState(initial.clientId || detail.clientId || '');
  const models = useApi<EntryModelDossier>(
    clientId ? `/accounting/entry-models/${clientId}` : null,
  );
  const [choice, setChoice] = useState(initial.modelId || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [source, setSource] = useState(detail);
  const available = (accounts.data || []).filter((a) => a.clientId === clientId);
  const sameBank = (a: Account) =>
    fold(a.bank) === fold(detail.statement.bankName) ||
    (!!detail.statement.bankCode &&
      banks.data?.some(
        (b) => b.id === a.bankId && fold(b.code) === fold(detail.statement.bankCode || ''),
      ));
  const entries = (models.data?.models || []).filter((m) => m.active);
  const documentAccountId = clientId === initial.clientId ? initial.bankAccountId : '';
  const ruleAccounts = available.filter(
    (a) =>
      (!documentAccountId || a.id === documentAccountId) &&
      sameBank(a) &&
      a.currency.trim().toUpperCase() === detail.statement.currency?.trim().toUpperCase(),
  );
  const eligibleEntries = entries.filter((model) => {
    const modelAccount = available.find((item) => item.id === model.bankAccountId);
    return documentAccountId
      ? model.bankAccountId === documentAccountId
      : !!modelAccount && sameBank(modelAccount);
  });
  const seeded = eligibleEntries.filter(
    (m) => m.bankAccountId === initial.bankAccountId && m.abbreviation === initial.abbreviation,
  );
  const selected =
    eligibleEntries.find((m) => m.id === choice) ||
    (!choice && seeded.length === 1 ? seeded[0] : undefined);
  const account = available.find((a) => a.id === selected?.bankAccountId);
  const compatibilityError = !selected
    ? ''
    : !account
      ? 'Le compte bancaire de cette écriture est indisponible pour cette entreprise.'
      : !sameBank(account)
        ? `Cette écriture concerne ${account.bank}, alors que le document importé concerne ${detail.statement.bankName}. Choisissez une écriture de ${detail.statement.bankName}, ou importez un document de ${account.bank}.`
        : account.currency.trim().toUpperCase() !==
            (detail.statement.currency || '').trim().toUpperCase()
          ? `Cette écriture utilise ${account.currency}, mais le document est en ${detail.statement.currency}. Choisissez une écriture dans la devise du document.`
          : selected.bankAccountBased && selected.accountCode !== account.accountCode
            ? 'Le compte comptable de la banque a changé. Actualisez les paramètres de cette écriture.'
            : '';
  const selection: EntryFlowSelection = {
    clientId,
    bankAccountId: selected?.bankAccountId || '',
    exportTemplateId: selected?.exportTemplateId || '',
    abbreviation: selected?.abbreviation || '',
    modelId: selected?.id || '',
    ...(formatChoice ? { outputTemplateId: formatChoice } : {}),
  };
  const outputFormatError =
    formatChoice &&
    !formats.loading &&
    !formats.data?.some((t) => t.id === formatChoice && t.active)
      ? 'Le modèle d’affichage choisi est indisponible ou inactif. Choisissez un format actif.'
      : '';
  const loading =
    clients.loading || accounts.loading || banks.loading || models.loading || formats.loading;
  const failure =
    error || clients.error || accounts.error || banks.error || models.error || formats.error;
  async function saveRule(rule: EntryModel) {
    if (!models.data || !format || busy) return;
    setBusy(true);
    setError('');
    try {
      await request(`/accounting/entry-models/${clientId}`, {
        method: 'PUT',
        body: JSON.stringify({
          version: models.data.version,
          models: [...models.data.models, rule],
        }),
      });
      setChoice(rule.id);
      setDraftRule(null);
      models.reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  async function proceed() {
    if (!selected || !account || compatibilityError || outputFormatError || busy) return;
    setBusy(true);
    setError('');
    try {
      if (source.clientId !== clientId || source.bankAccountId !== account.id) {
        const associated = await request<StatementDetail>(
          `/statement-imports/${detail.id}/association`,
          {
            method: 'PUT',
            body: JSON.stringify({ version: source.version, clientId, bankAccountId: account.id }),
          },
        );
        setSource(associated);
      }
      const preview = await request<EntryModelPreview>('/accounting/entry-models/preview', {
        method: 'POST',
        body: JSON.stringify({
          clientId,
          statementId: detail.id,
          exportTemplateId: selection.exportTemplateId,
          abbreviation: selection.abbreviation,
          modelId: selection.modelId,
          ...(selection.outputTemplateId ? { outputTemplateId: selection.outputTemplateId } : {}),
          ...(extractId ? { extractId } : {}),
          decisions: {},
        }),
      });
      onReview(selection, preview);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section
      className={embedded ? 'entry-model-inline' : 'ui-card conversion-model-step'}
      aria-label="Choix de l’écriture de l’entreprise"
    >
      {!embedded && (
        <>
          <ConversionSteps
            current={2}
            accountingMode
            extractMode={detail.statement.conversionType === 'EXTRACT'}
            disabled={busy}
            onNavigate={() => onImport()}
          />
          <div className="model-step-heading">
            <span className="model-step-icon">
              <BookOpen size={26} />
            </span>
            <div>
              <p className="conversion-eyebrow">ÉCRITURES COMPTABLES</p>
              <h2>Choisissez le modèle d’écritures</h2>
              <p>
                Choisissez un format d’exportation, puis la règle de compte et de TVA à appliquer au
                relevé.
              </p>
            </div>
          </div>
          <div className="model-step-document">
            <strong>{detail.sourceFileName || detail.statement.bankName}</strong>
            <span>
              {detail.statement.bankName} · {detail.statement.currency}
            </span>
          </div>
        </>
      )}
      {embedded && (
        <header className="entry-model-inline-heading">
          <div className="model-step-icon">
            <BookOpen size={21} />
          </div>
          <div>
            <h3>Règle comptable du client</h3>
            <p>Choisissez ou créez la règle à appliquer aux mouvements du relevé.</p>
          </div>
        </header>
      )}
      <>
        {!embedded && (
          <label className="conversion-template-label">
            Modèle d’exportation des écritures
            <select
              className="ui-input mt-2"
              value={formatChoice}
              disabled={busy || formats.loading}
              onChange={(e) => setFormatChoice(e.target.value)}
            >
              <option value="">
                {selected ? 'Format enregistré dans la règle' : 'Choisir un modèle d’exportation…'}
              </option>
              {formats.data
                ?.filter((t) => t.active)
                .map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
            </select>
          </label>
        )}
        {(!embedded || !detail.clientId) && (
          <label className="conversion-template-label">
            Entreprise
            <select
              aria-label="Entreprise des écritures"
              className="ui-input mt-2"
              value={clientId}
              disabled={busy || clients.loading}
              onChange={(e) => {
                setClientId(e.target.value);
                setChoice('');
                setError('');
              }}
            >
              <option value="">Choisir une entreprise…</option>
              {clients.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {embedded && detail.clientId && (
          <p className="entry-model-account-context">
            {clients.data?.find((item) => item.id === clientId)?.name || 'Entreprise sélectionnée'}
            {initial.bankAccountId && account
              ? ` · ${account.bank} · ${account.accountName}`
              : ` · ${detail.statement.bankName}`}
          </p>
        )}
        {eligibleEntries.length > 0 && (
          <label className="conversion-template-label">
            Règle comptable du client
            <select
              aria-label="Règle comptable du client"
              className="ui-input mt-2"
              value={selected?.id || ''}
              disabled={busy || loading || !clientId}
              onChange={(e) => setChoice(e.target.value)}
            >
              <option value="">Choisir une règle du client…</option>
              {eligibleEntries.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.abbreviation} — {m.name} · {m.accountCode} · TVA {m.taxRate} % ·{' '}
                  {available.find((a) => a.id === m.bankAccountId)?.bank || 'Compte indisponible'} ·{' '}
                  {m.keywords.join(', ')}
                </option>
              ))}
            </select>
          </label>
        )}
        {selected && (
          <div className="model-step-document">
            <strong>
              {selected.abbreviation} · {account?.accountName}
            </strong>
            <span>
              {selected.taxRate} % · {selected.keywords.join(', ')}
            </span>
          </div>
        )}
        {!loading && !failure && clientId && !eligibleEntries.length && (
          <p className="conversion-alert warning">
            {ruleAccounts.length ? (
              format ? (
                'Créez la première règle de ce client pour ce compte.'
              ) : (
                'Choisissez un format d’exportation pour créer la première règle de ce compte.'
              )
            ) : (
              <>
                Ajoutez un compte de cette banque dans la devise du document dans{' '}
                <Link className="underline" href="/banks">
                  Banques et comptes
                </Link>
                .
              </>
            )}
          </p>
        )}
        <div className="entry-model-actions">
          {clientId && ruleAccounts.length > 0 && (
            <Button
              permission="documents.write"
              variant="outline"
              disabled={!format || !models.data || busy}
              onClick={() => {
                setError('');
                setDraftRule(newEntryRule(format));
              }}
            >
              Créer une règle pour ce client
            </Button>
          )}
          <Button
            size="sm"
            variant="outline"
            disabled={busy || loading}
            onClick={() => {
              models.reload();
              accounts.reload();
              banks.reload();
              formats.reload();
              onReload();
            }}
          >
            Actualiser les écritures
          </Button>
        </div>
      </>
      {!loading && compatibilityError && (
        <p className="conversion-alert warning" role="alert">
          {compatibilityError}
        </p>
      )}
      {!loading && outputFormatError && (
        <p className="conversion-alert warning" role="alert">
          {outputFormatError}
        </p>
      )}
      {loading && <p role="status">Chargement des écritures…</p>}
      {failure && (
        <div className="conversion-alert error" role="alert">
          {failure}
          <Button
            variant="outline"
            onClick={() => {
              clients.reload();
              accounts.reload();
              banks.reload();
              models.reload();
              formats.reload();
              onReload();
              setError('');
            }}
          >
            Réessayer
          </Button>
        </div>
      )}
      <div className="model-step-footer">
        {embedded ? (
          <Button variant="outline" disabled={busy} onClick={onBack}>
            Retour aux modèles d’export
          </Button>
        ) : (
          <Button variant="outline" disabled={busy} onClick={onImport}>
            Retour à l’import
          </Button>
        )}
        <Button
          permission="documents.write"
          disabled={busy || loading || !selected || !!compatibilityError || !!outputFormatError}
          onClick={() => void proceed()}
        >
          <ArrowRight size={16} />
          {busy ? 'Préparation…' : 'Vérifier le résultat'}
        </Button>
      </div>
      {draftRule && (
        <EntryModelEditor
          initial={draftRule}
          forConversion
          accounts={ruleAccounts}
          formats={formats.data || []}
          busy={busy || loading}
          error={failure || outputFormatError}
          onReload={() => {
            setError('');
            models.reload();
            accounts.reload();
            formats.reload();
          }}
          onClose={() => {
            setDraftRule(null);
            setError('');
          }}
          onSave={saveRule}
        />
      )}
    </section>
  );
}
