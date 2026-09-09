import { useEffect, useState } from 'react';
import { Link } from '../../routing/router';
import {
  getPublicInventorySummary,
  type PublicInventorySummary,
} from '../api/publicInventory';
import { MediaOwnerLogo } from '../components/MediaOwnerLogo';
import { getPublicInventoryChannelPresentation, publicInventoryCountLabel } from '../data/publicInventoryChannels';

type NetworkState =
  | { status: 'loading' }
  | { status: 'ready'; data: PublicInventorySummary }
  | { status: 'unavailable' };

type Units = PublicInventorySummary['channels'][number]['units'];

export function PublicMediaNetworkPage({ channel }: { channel: string }) {
  const network = useNetworkState();
  const presentation = getPublicInventoryChannelPresentation(channel);
  const units = unitsFor(network, channel);
  const countBasis = network.status === 'ready'
    ? publicInventoryCountLabel(network.data.channels.find(item => item.channel === channel)?.countBasis ?? '', channel)
    : presentation.directoryTitle;
  return <section className="media-network-page" aria-labelledby="media-network-page-title">
    <div className="shell">
      <Link className="media-network-page__back" href="/">← Back to the media network</Link>
      <header className="media-network-page__header">
        <span className="eyebrow">ACTIVE PUBLISHED INVENTORY</span>
        <h1 id="media-network-page-title">{countBasis}</h1>
        <p>{networkDescription(network, units.length, countBasis)}</p>
      </header>
      <NetworkStateMessage state={network} hasUnits={units.length > 0} />
      {units.length > 0 && <UnitGrid units={units} label={countBasis} />}
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

function unitsFor(state: NetworkState, channel: string): Units {
  return state.status === 'ready'
    ? state.data.channels.find((item) => item.channel === channel)?.units ?? []
    : [];
}

function networkDescription(state: NetworkState, count: number, countBasis: string) {
  if (state.status !== 'ready') {
    return 'Loading media represented by current published catalogue records.';
  }
  return `${countBasis}: ${count.toLocaleString()} represented by current published catalogue records. Counts refer to the stated media units, not their parent owners. Product counts do not establish distinct physical sites.`;
}

function NetworkStateMessage({ state, hasUnits }: {
  state: NetworkState; hasUnits: boolean;
}) {
  if (state.status === 'loading') {
    return <div className="media-network-page__state" role="status">Loading media directory…</div>;
  }
  if (state.status === 'unavailable') {
    return <div className="media-network-page__state" role="alert">The current media directory is temporarily unavailable.</div>;
  }
  return hasUnits ? null
    : <div className="media-network-page__state">No identifiable media units are currently available in this directory.</div>;
}

function UnitGrid({ units, label }: { units: Units; label: string }) {
  return <div className="media-network-owner-grid" aria-label={label}>
    {units.map((unit) => <MediaOwnerLogo key={unit.id}
      name={unit.name} logoUrl={unit.logoUrl} />)}
  </div>;
}
