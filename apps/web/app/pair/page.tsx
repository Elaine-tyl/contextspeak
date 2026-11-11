"use client";
import { useEffect, useState } from "react";
import { API, api, type WebSession } from "../../src/api";
export default function Pair() {
  const [code, setCode] = useState("");
  const [sessionChecked, setSessionChecked] = useState(false);
  const [session, setSession] = useState<WebSession | null>(null);
  const [message, setMessage] = useState("");
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    setCode(new URL(location.href).searchParams.get("code") ?? "");
    void api<WebSession>("", "/auth/session")
      .then(setSession)
      .catch(() => {})
      .finally(() => setSessionChecked(true));
  }, []);
  async function approve() {
    if (!session) return;
    setBusy(true);
    try {
      await api(session.csrfToken, "/auth/pairings/approve", "POST", {
        userCode: code,
      });
      setDone(true);
      setMessage(
        "Extension approved. Return to the extension to finish pairing.",
      );
    } catch {
      setMessage(
        "Code expired, session changed, or already used. Start pairing again from the extension.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="page page-narrow pair-page">
      <a className="back-link" href="/library">
        Back to library
      </a>
      <h1>Pair your extension</h1>
      <p className="page-lead">
        Connect the ContextSpeak extension to your account so the words you look
        up are saved here.
      </p>
      <section className="panel pair-panel">
        <ol className="pair-steps">
          <li>
            In the extension, click <strong>Pair extension</strong>.
          </li>
          <li>Check that the code below matches the one in the extension.</li>
          <li>
            Click <strong>Approve extension</strong>.
          </li>
        </ol>
        <p className="notice">
          Only approve a code you started yourself. Never approve a code someone
          sent you.
        </p>
        <label className="pair-code">
          Pairing code
          <input
            aria-label="Pairing code"
            value={code}
            maxLength={12}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            disabled={done}
            autoComplete="off"
            spellCheck={false}
          />
        </label>
        {session ? (
          <>
            <p className="muted">
              Signing in as <strong>{session.identity.subject}</strong>. The
              extension can translate, save lookups (when you turn that on) and
              read your library for 30 days. You can revoke it any time in
              Library settings.
            </p>
            <button
              className="primary"
              disabled={done || busy || !/^[A-F0-9]{12}$/.test(code)}
              onClick={approve}
            >
              Approve extension
            </button>
          </>
        ) : sessionChecked ? (
          <p>
            <a
              className="primary"
              href={
                API +
                "/auth/login" +
                (/^[A-F0-9]{12}$/.test(code) ? "?pair=" + code : "")
              }
            >
              Sign in with Google
            </a>
          </p>
        ) : (
          <p>Checking sign-in…</p>
        )}
        <p role="status">{message}</p>
      </section>
    </main>
  );
}
