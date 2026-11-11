"use client";
import { useEffect, useState } from "react";

const LOCAL = "http://127.0.0.1:4320";

export default function WorldPage() {
  const [ready, setReady] = useState(false);
  const [checked, setChecked] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    fetch(LOCAL + "/health", {
      signal: AbortSignal.any([controller.signal, AbortSignal.timeout(3000)]),
    })
      .then((response) => setReady(response.ok))
      .catch(() => {})
      .finally(() => {
        if (!controller.signal.aborted) setChecked(true);
      });
    return () => controller.abort();
  }, []);
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
        <h2>Your first day</h2>
        <p>
          Help your family with dinner, walk to Green Market for groceries, then
          order a drink and catch up with a friend. Menus, your bag and wallet
          follow the conversation. Stop recording for automatic feedback. If
          wording needs a correction, say it again before continuing; typing is
          rehearsal only.
        </p>
        <p role="status">
          {!checked
            ? "Checking local launcher…"
            : ready
              ? "Local launcher is ready."
              : "Start the local launcher, then refresh this page."}
        </p>
        <div className="detail-actions">
          <a
            className="primary"
            href={LOCAL + "/unity/?world=life"}
            target="_blank"
            rel="noreferrer"
          >
            Play Life town
          </a>
          <a className="secondary" href={LOCAL + "/download/macos"}>
            Download macOS preview
          </a>
        </div>
        <p className="badge">
          Unity 3D · Local guest save · Family home, market and cafe
        </p>
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
          WASD / arrows to walk, E to talk, right-drag to look, scroll to zoom,
          Esc to explore. Walk to objective finds a route; movement keys cancel
          it.
        </p>
        <p>
          Keep the local launcher, Ollama and installed speech models running.
          Initial voice setup uses <code>pnpm world:setup</code> and{" "}
          <code>pnpm speech:setup</code>. This desktop preview requires the
          companion; it is not a self-contained installer.
        </p>
      </details>
      <p className="section-copy">
        School, farming, clothing, restaurants, fishing, amusement parks and
        travel are planned districts. Task feedback is separate from acoustic
        pronunciation assessment and vocabulary reviews.
      </p>
    </main>
  );
}
