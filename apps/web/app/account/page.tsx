"use client";
import { API } from "../../src/api";
import { useAccount } from "./useAccount";
import "./account.css";
export default function AccountPage() {
  const account = useAccount();
  return (
    <main className="page page-narrow account-page">
      <div className="page-head">
        <div>
          <h1>Account</h1>
          <p className="page-lead">
            Connect the browser extension, download a copy of your data, or
            delete your account.
          </p>
        </div>
      </div>
      {account.deleted ? (
        <section className="account-panel" aria-live="polite">
          <h2>Your account has been deleted</h2>
          <p>
            Your saved learning data has been removed and all website and
            extension sessions have been revoked.
          </p>
          {account.deleted.cleanupPending && (
            <p>
              Private generated audio cleanup is pending and will retry
              automatically.
            </p>
          )}
          <p>
            Signing in again creates a fresh account. Your downloaded exports
            stay on your device; delete those separately if needed.
          </p>
        </section>
      ) : account.session ? (
        <>
          <p className="account-identity">
            <span className="status-dot online" aria-hidden="true" /> Signed in
            as <strong>{account.session.identity.subject}</strong>
          </p>
          <section className="account-panel">
            <h2>Browser extension</h2>
            <p>
              The extension translates words as you read and saves them to your
              library. Pair it once on each browser you use.
            </p>
            <div className="account-actions">
              <a className="secondary" href="/pair">
                Pair an extension
              </a>
              <a className="link-button" href="/library">
                Manage in Library settings
              </a>
            </div>
          </section>
          <section className="account-panel">
            <h2>Download your data</h2>
            <p>
              Save a JSON copy of your vocabulary, lookup history, review
              schedule, practice, games, and learning preferences. Generated
              audio is listed as metadata.
            </p>
            <p className="fine-print">
              Exports can contain private text and page addresses. Store your
              download somewhere safe.
            </p>
            <button
              className="primary"
              disabled={!!account.busy}
              onClick={() => void account.run("export")}
            >
              {account.busy === "export"
                ? "Preparing export…"
                : "Download my data"}
            </button>
          </section>
          <section className="account-panel account-danger">
            <h2>Delete your account</h2>
            <p>
              This permanently removes your saved learning data and signs out
              every connected website and extension session. This cannot be
              undone.
            </p>
            <p className="fine-print">
              Generated audio is removed from active storage, with automatic
              retries if storage is unavailable. Backups may retain data until
              they expire.
            </p>
            <label htmlFor="delete-confirmation">
              Type <strong>DELETE</strong> to confirm
            </label>
            <input
              id="delete-confirmation"
              value={account.confirmation}
              onChange={(event) => account.setConfirmation(event.target.value)}
              autoComplete="off"
              spellCheck={false}
              disabled={!!account.busy}
            />
            <button
              className="danger-button"
              disabled={!!account.busy || account.confirmation !== "DELETE"}
              onClick={() => void account.run("delete")}
            >
              {account.busy === "delete"
                ? "Deleting account…"
                : "Delete my account permanently"}
            </button>
          </section>
        </>
      ) : (
        <section className="account-panel">
          <p>
            {account.checked
              ? "Sign in to manage your account and data."
              : "Checking sign-in…"}
          </p>
          {account.checked && (
            <a className="primary" href={API + "/auth/login?returnTo=/account"}>
              Sign in with Google
            </a>
          )}
        </section>
      )}
      <p className="account-status" role="status">
        {account.message}
      </p>
    </main>
  );
}
