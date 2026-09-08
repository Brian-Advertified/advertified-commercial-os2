import { useEffect, useState, type FormEvent } from 'react';

import { humanMessage } from '../api/client';
import { inventoryApi } from '../api/inventory-client';
import type { InventorySupplierLifecycle, SupplierClaimInvitation } from '../api/inventory-lifecycle-schemas';
import { masterDataCodes } from '../generated/master-data-codes';
import { notifications } from '../notifications/notifications';
import { formatDateTime, humanizeCode } from '../presentation/format';

export function SupplierClaimPanel({ tenantId, supplierId, token }: {
  tenantId: string; supplierId: string; token: string;
}) {
  const [lifecycle, setLifecycle] = useState<InventorySupplierLifecycle | null>(null);
  const [email, setEmail] = useState('');
  const [days, setDays] = useState(7);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [issued, setIssued] = useState<SupplierClaimInvitation | null>(null);

  useEffect(() => {
    let active = true;
    void inventoryApi.getSupplierLifecycle(tenantId, supplierId)
      .then((value) => { if (active) setLifecycle(value); })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)); });
    return () => { active = false; };
  }, [tenantId, supplierId]);

  async function issue(event: FormEvent) {
    event.preventDefault();
    if (!lifecycle || !email.trim()) return;
    setBusy(true); setError(null); setIssued(null);
    try {
      const invitation = await inventoryApi.issueSupplierClaimInvitation(
        tenantId, supplierId,
        { email: email.trim(), role: masterDataCodes.roles.supplierUser, validForDays: days },
        lifecycle.version,
        token,
      );
      setIssued(invitation);
      setLifecycle(await inventoryApi.getSupplierLifecycle(tenantId, supplierId));
      notifications.success('Supplier registration invitation created.');
    } catch (failure) {
      setError(humanMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  async function revoke(invitation: SupplierClaimInvitation) {
    const reason = window.prompt('Reason for revoking this invitation?')?.trim();
    if (!reason) return;
    setBusy(true); setError(null);
    try {
      await inventoryApi.revokeSupplierClaimInvitation(
        tenantId, invitation.id, invitation.version, reason, token);
      setLifecycle(await inventoryApi.getSupplierLifecycle(tenantId, supplierId));
      notifications.success('Supplier invitation revoked.');
    } catch (failure) {
      setError(humanMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  if (!lifecycle) return <section className="inventory-record-section"><p>Loading supplier access…</p></section>;
  const active = lifecycle.invitations.filter((item) => item.status === masterDataCodes.supplierInvitationStatuses.active);
  const link = issued?.registrationToken
    ? supplierClaimLink(tenantId, issued.id, issued.registrationToken)
    : null;

  return <SupplierClaimAccess lifecycle={lifecycle} active={active} issued={issued}
    email={email} setEmail={setEmail} days={days} setDays={setDays}
    busy={busy} error={error} link={link} issue={issue} revoke={revoke} />;
}

function SupplierClaimAccess(props: {
  lifecycle: InventorySupplierLifecycle; active: SupplierClaimInvitation[];
  issued: SupplierClaimInvitation | null; email: string; setEmail: (value: string) => void;
  days: number; setDays: (value: number) => void; busy: boolean; error: string | null;
  link: string | null; issue: (event: FormEvent) => Promise<void>;
  revoke: (invitation: SupplierClaimInvitation) => Promise<void>;
}) {
  return <section className="inventory-record-section" aria-labelledby="supplier-access-title">
    <p className="eyebrow">Supplier access</p><h2 id="supplier-access-title">Registration and inventory claim</h2>
    <p>{props.lifecycle.name} is {humanizeCode(props.lifecycle.claimStatus)}. Invite the verified supplier contact to sign in and claim only this supplier inventory.</p>
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <form className="record-form" onSubmit={(event) => void props.issue(event)}>
      <label>Email address<input type="email" required value={props.email} onChange={(event) => props.setEmail(event.target.value)} /></label>
      <label>Valid for days<input type="number" min={1} max={30} value={props.days} onChange={(event) => props.setDays(Number(event.target.value))} /></label>
      <button type="submit" disabled={props.busy || !props.email.trim()}>Create registration link</button>
    </form>
    {props.link && <div className="detail-card"><strong>Registration link</strong>
      <p>Send this link to {props.issued?.invitedEmail}. The claim token is kept in the URL fragment so it is not sent to the web server.</p>
      <input readOnly value={props.link} aria-label="Supplier registration link" />
      <button type="button" onClick={() => void navigator.clipboard.writeText(props.link!)}>Copy link</button>
    </div>}
    {props.active.length > 0 && <div><h3>Active invitations</h3>{props.active.map((invitation) =>
      <article className="detail-card" key={invitation.id}><strong>{invitation.invitedEmail}</strong>
        <p>Expires {formatDateTime(invitation.expiresAtUtc)}</p>
        <button type="button" disabled={props.busy} onClick={() => void props.revoke(invitation)}>Revoke</button>
      </article>)}</div>}
  </section>;
}

function supplierClaimLink(tenantId: string, invitationId: string, token: string): string {
  const path = `/supplier-claim/${tenantId}/${invitationId}#token=${encodeURIComponent(token)}`;
  return `${window.location.origin}${path}`;
}
