import { useEffect, useMemo, useState } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { z } from 'zod';

import { humanMessage } from '../api/client';
import { inventoryApi } from '../api/inventory-client';
import { useSession } from '../auth/session-state';
import { LoadingState, MessageState } from '../components/PageState';
import { notifications } from '../notifications/notifications';

export function SupplierClaimPage() {
  const params = useParams();
  const route = useMemo(() => z.object({
    tenantId: z.uuid(), invitationId: z.uuid(),
  }).safeParse(params), [params]);
  const { session, loading } = useSession();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tenantId = route.success ? route.data.tenantId : '';
  const invitationId = route.success ? route.data.invitationId : '';
  const storageKey = route.success ? `advertified:supplier-claim:${invitationId}` : '';

  useEffect(() => {
    if (!route.success) return;
    const fragment = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const token = fragment.get('token');
    if (token) {
      sessionStorage.setItem(storageKey, token);
      history.replaceState(null, '', `/supplier-claim/${tenantId}/${invitationId}`);
    }
  }, [route.success, storageKey, tenantId, invitationId]);

  if (!route.success) return <MessageState title="Invitation not found" message="Use the complete supplier registration link again." />;
  if (loading && !session) return <LoadingState label="Opening supplier registration" />;
  if (!session?.authenticated) {
    const returnTo = `/supplier-claim/${tenantId}/${invitationId}`;
    return <Navigate to={`/sign-in?returnTo=${encodeURIComponent(returnTo)}`} replace />;
  }

  const registrationToken = sessionStorage.getItem(storageKey);
  if (!registrationToken) {
    return <MessageState title="Registration token is missing"
      message="Open the original supplier registration link again. The claim token is not retained outside this browser session." />;
  }
  const activeSession = session;
  const claimToken = registrationToken;

  async function accept() {
    setBusy(true); setError(null);
    try {
      await inventoryApi.acceptSupplierClaimInvitation(
        tenantId, invitationId, claimToken, activeSession.antiforgeryToken);
      sessionStorage.removeItem(storageKey);
      notifications.success('Supplier inventory access is now connected to your account.');
      navigate('/workspaces', { replace: true });
    } catch (failure) {
      setError(humanMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return <main className="sign-in-page supplier-claim-page">
    <section className="sign-in-story"><div><p className="eyebrow eyebrow-light">Supplier registration</p>
      <h1>Connect your media inventory.</h1>
      <p>The invitation is restricted to the supplier record and email address selected by Advertified.</p>
    </div></section>
    <section className="sign-in-panel"><div className="sign-in-card">
      <p className="eyebrow">Inventory claim</p><h2>Confirm supplier access</h2>
      <p className="supporting-copy">Your verified sign-in email must match the invitation. Claiming does not grant access to any other supplier or workspace.</p>
      {error && <div className="inline-alert" role="alert">{error}</div>}
      <button className="primary-button" type="button" disabled={busy} onClick={() => void accept()}>
        {busy ? 'Connecting inventory…' : 'Claim supplier inventory'}
      </button>
    </div></section>
  </main>;
}
