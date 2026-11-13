const LOCAL = "http://127.0.0.1:4320";

export default function WorldPage() {
  return (
    <main className="page page-narrow">
      <a className="back-link" href="/games">
        Back to games
      </a>
      <h1>ContextSpeak Life</h1>
      <p className="page-lead">
        Explore your neighborhood, help your family and make plans with friends
        through spoken English.
      </p>
      <section className="panel">
        <h2>Forest café and fishing</h2>
        <p>
          Meet Rowan, browse the illustrated menu, customize your order and pay
          before it arrives at your table. Explore the terrace and learn to fish
          with Hazel. Choose an IELTS speaking practice band in Settings; the
          feedback is learning guidance, not an official test score.
        </p>
        <div className="detail-actions">
          <a
            className="primary"
            href={LOCAL + "/unity/?world=slice"}
            target="_blank"
            rel="noreferrer"
          >
            Play Forest café
          </a>
          <a className="secondary" href={LOCAL + "/download/macos"}>
            Download macOS preview
          </a>
        </div>
      </section>
      <section className="panel">
        <h2>More ways to practice</h2>
        <p>
          <a href={LOCAL + "/unity/"} target="_blank" rel="noreferrer">
            Original cafe mission
          </a>
          {" · "}
          <a href="/games">Godot 2D learning games</a>
          {" · "}
          <a href="/games/web">Accessible web games</a>
        </p>
        <p>
          The arcade saves scores to your account. Life is a separate local
          guest world.
        </p>
      </section>
      <details className="panel">
        <summary>Setup and controls</summary>
        <p>
          Install and activate Unity 6000.3.28f1 LTS with Web Build Support. Run{" "}
          <code>pnpm unity:prepare</code>, <code>pnpm unity:test</code>,{" "}
          <code>pnpm world:build</code> and <code>pnpm world:dev</code>. Build
          the desktop download with <code>pnpm unity:build:mac</code>; build the
          arcade with <code>pnpm arcade:build</code>.
        </p>
        <p>
          WASD / arrows to move, F to talk and E to use objects. The forest café
          adds Ctrl to walk, Shift to sprint, C to crouch, M for the map, J for
          the journal and Tab for your bag. In Life town, Walk to objective
          finds a route; movement keys cancel it.
        </p>
        <p>
          Keep the local launcher, Ollama and installed speech models running.
          Initial voice setup uses <code>pnpm world:setup</code> and{" "}
          <code>pnpm speech:setup</code>. This desktop preview requires the
          companion; it is not a self-contained installer.
        </p>
      </details>
      <p className="section-copy">
        School, farming, clothing, restaurants, amusement parks and travel are
        planned districts. Task feedback is separate from acoustic pronunciation
        assessment and vocabulary reviews.
      </p>
    </main>
  );
}
