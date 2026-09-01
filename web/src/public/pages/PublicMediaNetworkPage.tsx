import { useEffect, useState } from 'react';
import { Link } from '../../routing/router';
import {
  getPublicInventorySummary,
  type PublicInventorySummary,
} from '../api/publicInventory';
import { MediaOwnerLogo } from '../components/MediaOwnerLogo';
import { getPublicInventoryChannelPresentation } from '../data/publicInventoryChannels';

type NetworkState =
  | { status: 'loading' }
  | { status: 'ready'; data: PublicInventorySummary }
  | { status: 'unavailable' };

type Owners = PublicInventorySummary['channels'][number]['owners'];

export function PublicMediaNetworkPage({ channel }: { channel: string }) {
  const network = useNetworkState();
  const presentation = getPublicInventoryChannelPresentation(channel);
  const owners = ownersFor(network, channel);
  return <section className="media-network-page" aria-labelledby="media-network-page-title">
    <div className="shell">
      <Link className="media-network-page__back" href="/">← Back to the media network</Link>
      <header className="media-network-page__header">
        <span className="eyebrow">ACTIVE PUBLISHED INVENTORY</span>
        <h1 id="media-network-page-title">{presentation.directoryTitle}</h1>
        <p>{networkDescription(network, owners.length)}</p>
      </header>
      <NetworkStateMessage state={network} hasOwners={owners.length > 0} />
      {owners.length > 0 && <OwnerGrid owners={owners} label={presentation.label} />}
    </div>
  </section>;
}

function useNetworkState(): NetworkState {
  const [network, setNetwork] = useState<NetworkState>({ status: 'loading' });
  useEffect(() => {
    const controller = new AbortController();
    getPublicInventorySummary(controller.signal)
      .then((data) => setNetwork({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          setNetwork({ status: 'unavailable' });
        }
      });
    return () => controller.abort();
  }, []);
  return network;
}

function ownersFor(state: NetworkState, channel: string): Owners {
  return state.status === 'ready'
    ? state.data.channels.find((item) => item.channel === channel)?.owners ?? []
    : [];
}

function networkDescription(state: NetworkState, count: number) {
  if (state.status !== 'ready') {
    return 'Loading the media owners represented by current published catalogue records.';
  }
  const suffix = count === 1 ? '' : 's';
  return `${count.toLocaleString()} distinct media owner${suffix} represented by current published catalogue records.`;
}

function NetworkStateMessage({ state, hasOwners }: {
  state: NetworkState; hasOwners: boolean;
}) {
  if (state.status === 'loading') {
    return <div className="media-network-page__state" role="status">Loading media owners…</div>;
  }
  if (state.status === 'unavailable') {
    return <div className="media-network-page__state" role="alert">The current media owner directory is temporarily unavailable.</div>;
  }
  return hasOwners ? null
    : <div className="media-network-page__state">No active published owners are currently listed for this channel.</div>;
}

function OwnerGrid({ owners, label }: { owners: Owners; label: string }) {
  return <div className="media-network-owner-grid" aria-label={`${label} media owners`}>
    {owners.map((owner) => <MediaOwnerLogo key={owner.id}
      name={owner.name} logoUrl={owner.logoUrl} />)}
  </div>;
}
