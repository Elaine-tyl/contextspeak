"use client";
import { useEffect, useState } from "react";
import { API, api, type WebSession } from "../../src/api";
export default function Pair() {
  const [code, setCode] = useState("");
  const [session, setSession] = useState<WebSession | null>(null);
  const [message, setMessage] = useState("");
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    setCode(new URL(location.href).searchParams.get("code") ?? "");
    void api<WebSession>("", "/auth/session")
      .then(setSession)
      .catch(() => {});
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
    <main
      style={{
        maxWidth: 560,
        margin: "60px auto",
        padding: 24,
        fontFamily: "system-ui",
        lineHeight: 1.6,
      }}
    >
      <h1>Pair your extension</h1>
      <p>
        Only approve if you just started pairing from your own ContextSpeak
        extension. Match this code with the code shown there. Never approve a
        code sent by someone else.
      </p>
      <label>
        Pairing code
        <input
          aria-label="Pairing code"
          value={code}
          maxLength={12}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          disabled={done}
          style={{ display: "block", padding: 12, fontSize: 22, width: "90%" }}
        />
      </label>
      {session ? (
        <>
          <p>Account: {session.identity.subject}</p>
          <p>
            This extension can translate, collect queries when enabled, and
            access your library for 30 days. You can revoke access from the
            library.
          </p>
          <button
            disabled={done || busy || !/^[A-F0-9]{12}$/.test(code)}
            onClick={approve}
          >
            Approve extension
          </button>
        </>
      ) : (
        <p>
          <a
            href={
              API +
              "/auth/login" +
              (/^[A-F0-9]{12}$/.test(code) ? "?pair=" + code : "")
            }
          >
            Sign in with Google
          </a>
        </p>
      )}
      <p role="status">{message}</p>
      <a href="/library">Back to library</a>
    </main>
  );
}
