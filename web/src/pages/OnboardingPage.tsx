import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';

import { humanMessage } from '../api/client';
import { publicIntakeApi, type PublicIntake } from '../api/public-intake-client';
import { useSession } from '../auth/session-state';
import { useWorkspace } from '../auth/workspace-state';
import { LoadingState, MessageState } from '../components/PageState';
import { masterDataCodes } from '../generated/master-data-codes';
import { notifications } from '../notifications/notifications';
import { formatDateTime, humanizeCode } from '../presentation/format';

const registrationTypes = new Set<string>([
  masterDataCodes.publicIntakeTypes.advertiser,
  masterDataCodes.publicIntakeTypes.agency,
  masterDataCodes.publicIntakeTypes.mediaOwner,
  masterDataCodes.publicIntakeTypes.creator,
]);

export function OnboardingPage() {
  const { selected, loading } = useWorkspace();
  const { session } = useSession();
  if (loading) return <LoadingState />;
  if (!selected) return <Navigate to="/workspaces" replace />;
  if (selected.roleCode !== masterDataCodes.roles.platformAdmin) {
    return <MessageState title="Onboarding is restricted" message="Platform administration access is required." />;
  }
  if (!session) return <LoadingState />;
  return <OnboardingWorkspace tenantId={selected.tenantId} token={session.antiforgeryToken} />;
}

function OnboardingWorkspace({ tenantId, token }: { tenantId: string; token: string }) {
  const [items, setItems] = useState<PublicIntake[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    void publicIntakeApi.list(tenantId, masterDataCodes.lifecycleStatuses.pending)
      .then((page) => { if (active) setItems(page.items); })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)); });
    return () => { active = false; };
  }, [tenantId]);

  if (error && !items) return <MessageState title="Onboarding could not be loaded" message={error} />;
  if (!items) return <LoadingState label="Loading onboarding requests" />;

  const update = (value: PublicIntake) => {
    setItems((current) => current?.filter((item) => item.id !== value.id) ?? []);
  };
  const run = async (item: PublicIntake, action: () => Promise<PublicIntake>) => {
    setBusyId(item.id); setError(null);
    try {
      const updated = await action();
      update(updated);
      notifications.success('The onboarding decision was recorded.');
    } catch (failure) {
      setError(humanMessage(failure));
    } finally {
      setBusyId(null);
    }
  };

  return <section className="operations-page" aria-labelledby="onboarding-title">
    <header className="operations-command-header"><div>
      <p className="eyebrow">Market entry</p><h1 id="onboarding-title">Onboarding requests</h1>
      <p>Review public enquiries and provision verified organisations into canonical Advertified workspaces.</p>
    </div></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <dl className="operations-context-strip"><div><dt>Pending requests</dt><dd>{items.length}</dd></div>
      <div><dt>Registrations</dt><dd>{items.filter((item) => registrationTypes.has(item.typeCode)).length}</dd></div>
      <div><dt>Enquiries</dt><dd>{items.filter((item) => !registrationTypes.has(item.typeCode)).length}</dd></div></dl>
    <section className="operations-panel" aria-labelledby="onboarding-queue-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Review queue</p>
        <h2 id="onboarding-queue-title">Requests waiting for a decision</h2></div><span>{items.length} pending</span></header>
      {items.length === 0 ? <div className="operations-empty-row"><strong>Queue clear</strong>
        <p>No public requests are waiting for review.</p></div> :
        <div className="onboarding-request-grid">{items.map((item) =>
          <IntakeCard key={item.id} item={item} busy={busyId === item.id}
            provision={(values) => run(item, () => publicIntakeApi.provision(tenantId, item, values, token))}
            reject={(reason) => run(item, () => publicIntakeApi.reject(tenantId, item, reason, token))}
            resolve={(reason) => run(item, () => publicIntakeApi.resolve(tenantId, item, reason, token))} />)}</div>}
    </section>
  </section>;
}

function IntakeCard({ item, busy, provision, reject, resolve }: {
  item: PublicIntake; busy: boolean;
  provision: (values: { legalName: string; tradingName: string; website: string | null; vatNumber: string | null; requireMfa: boolean; reason: string }) => Promise<void>;
  reject: (reason: string) => Promise<void>;
  resolve: (reason: string) => Promise<void>;
}) {
  const registration = registrationTypes.has(item.typeCode);
  const [legalName, setLegalName] = useState(item.organisation);
  const [tradingName, setTradingName] = useState(item.organisation);
  const [website, setWebsite] = useState(item.website ?? '');
  const [vatNumber, setVatNumber] = useState('');
  const [requireMfa, setRequireMfa] = useState(true);
  const [reason, setReason] = useState('');
  const validReason = reason.trim().length >= 5;

  return <article className="detail-card onboarding-request-card">
    <header><div><span className="operations-state-label">{humanizeCode(item.typeCode, true)}</span>
      <h3>{item.organisation}</h3><p>{item.name} · {item.email}</p></div>
      <small>{formatDateTime(item.createdAtUtc)}</small></header>
    {item.phone && <p><strong>Phone:</strong> {item.phone}</p>}
    {item.relationship && <p><strong>Relationship:</strong> {item.relationship}</p>}
    {item.message && <p><strong>Context:</strong> {item.message}</p>}
    {registration && <div className="onboarding-provision-fields">
      <label>Legal name<input value={legalName} onChange={(event) => setLegalName(event.target.value)} /></label>
      <label>Trading name<input value={tradingName} onChange={(event) => setTradingName(event.target.value)} /></label>
      <label>Website<input value={website} onChange={(event) => setWebsite(event.target.value)} /></label>
      <label>VAT number<input value={vatNumber} onChange={(event) => setVatNumber(event.target.value)} /></label>
      <label className="checkbox-row"><input type="checkbox" checked={requireMfa} onChange={(event) => setRequireMfa(event.target.checked)} /> Require MFA</label>
    </div>}
    <label>Decision reason<textarea value={reason} maxLength={1000} onChange={(event) => setReason(event.target.value)} /></label>
    <div className="action-row">
      {registration ? <>
        <button type="button" disabled={busy || !validReason || !legalName.trim() || !tradingName.trim()}
          onClick={() => void provision({ legalName, tradingName, website: website.trim() || null,
            vatNumber: vatNumber.trim() || null, requireMfa, reason })}>Provision workspace</button>
        <button type="button" disabled={busy || !validReason} onClick={() => void reject(reason)}>Reject registration</button>
      </> : <button type="button" disabled={busy || !validReason} onClick={() => void resolve(reason)}>Mark enquiry resolved</button>}
    </div>
  </article>;
}
