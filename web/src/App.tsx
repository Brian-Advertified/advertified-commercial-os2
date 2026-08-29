import './App.css'

const foundationServices = [
  { name: 'Commercial API', owner: 'C# / .NET 8', state: 'Health baseline' },
  { name: 'Agent runtime', owner: 'Python / FastAPI', state: 'Provider disabled' },
  { name: 'Web application', owner: 'React / TypeScript', state: 'Build baseline' },
  { name: 'Data platform', owner: 'PostgreSQL 16', state: 'Extensions verified locally' },
] as const

const deliveryControls = [
  'Canonical writes stay in the Commercial API',
  'AI output is proposal data, never authority',
  'Commercial actions require named human approval',
  'Unknown facts remain unknown',
] as const

function BrandRail() {
  return (
    <aside className="brand-rail" aria-label="Advertified foundation navigation">
      <a className="brand-mark" href="/" aria-label="Advertified home">A</a>
      <nav>
        <a className="nav-item nav-item-active" href="#overview">Overview</a>
        <a className="nav-item" href="#services">Services</a>
        <a className="nav-item" href="#controls">Controls</a>
      </nav>
      <span className="environment-pill">Local only</span>
    </aside>
  )
}

function WorkspaceHeader() {
  return (
    <header className="workspace-header">
      <div>
        <p className="eyebrow">Advertified Commercial OS</p>
        <h1>Development foundation</h1>
      </div>
      <span className="gate-badge">Gate 0 baseline</span>
    </header>
  )
}

function ServiceCard({ name, owner, state }: (typeof foundationServices)[number]) {
  return (
    <article className="service-card">
      <span className="status-dot" aria-hidden="true" />
      <div>
        <h3>{name}</h3>
        <p>{owner}</p>
      </div>
      <span className="service-state">{state}</span>
    </article>
  )
}

function FoundationDashboard() {
  return (
    <main className="workspace">
      <WorkspaceHeader />
      <section className="hero-panel" id="overview">
        <p className="eyebrow">Evidence before expansion</p>
        <h2>A stable base for every campaign journey.</h2>
        <p>Product modules remain gated until their contracts, tests, and human decisions are approved.</p>
      </section>
      <section id="services" aria-labelledby="services-title">
        <div className="section-heading">
          <div><p className="eyebrow">Runtime map</p><h2 id="services-title">Foundation services</h2></div>
          <span>Scaffold status—not feature completion</span>
        </div>
        <div className="service-grid">
          {foundationServices.map((service) => <ServiceCard key={service.name} {...service} />)}
        </div>
      </section>
      <section className="controls-panel" id="controls" aria-labelledby="controls-title">
        <div><p className="eyebrow">Non-negotiable</p><h2 id="controls-title">Delivery controls</h2></div>
        <ul>{deliveryControls.map((control) => <li key={control}>{control}</li>)}</ul>
      </section>
    </main>
  )
}

function App() {
  return <div className="app-shell"><BrandRail /><FoundationDashboard /></div>
}

export default App
