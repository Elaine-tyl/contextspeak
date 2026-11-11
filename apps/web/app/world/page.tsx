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
      <h1>3D conversation world</h1>
      <p className="page-lead">
        Order two drinks with Maya, then sit with Alex for a voiced
        conversation.
      </p>
      <section className="panel">
        <h2>Your first cafe mission</h2>
        <p>
          Choose your drinks, answer the barista’s questions, and confirm your
          order. Take your drinks to Alex’s table, share your preferences, make
          a weekend plan and invite your friend. NPC questions play aloud
          automatically; speak or type your reply.
        </p>
        <p>
          Move with WASD or arrow keys. Walk to the counter and press E to talk.
          Recording stops after 20 seconds and your reply is checked
          automatically.
        </p>
        <p className="badge">
          Local prototype · English · Guest progress on this device
        </p>
        <p role="status">
          {!checked
            ? "Checking local launcher…"
            : ready
              ? "Local launcher is ready."
              : "Start the local launcher, then refresh this page."}
        </p>
        <div className="detail-actions">
          <a className="primary" href={LOCAL} target="_blank" rel="noreferrer">
            Play in browser
          </a>
          <a className="secondary" href={LOCAL + "/download/macos"}>
            Download macOS prototype
          </a>
        </div>
      </section>
      <details className="panel">
        <summary>Prototype setup and limits</summary>
        <p>
          From your ContextSpeak project, run <code>pnpm world:setup</code>,{" "}
          <code>pnpm world:build</code>, then <code>pnpm world:dev</code>. Keep
          the local launcher and Ollama running for either version.
        </p>
        <p>
          AI runs on your computer. Initial setup downloads free tools/models;
          there is no cloud API fallback. The desktop download is a development
          build, not a self-contained installer.
        </p>
        <p>
          Task-goal feedback is separate from pronunciation assessment, your
          vocabulary reviews and your account Dashboard. School, amusement-park
          scenes and additional difficulty levels will follow this first
          prototype.
        </p>
      </details>
    </main>
  );
}
