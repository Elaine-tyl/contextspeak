"use client";
import Link from "next/link";
import { API } from "../../src/api";
import { useAccount } from "./useAccount";
import "./account.css";
export default function AccountPage() {
  const account = useAccount();
  return (
    <main className="account-page">
      <Link href="/library" className="account-back">
        ← Library
      </Link>
      <p className="account-eyebrow">YOUR ACCOUNT</p>
      <h1>Your data, your choice</h1>
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
            Signed in as {account.session.identity.subject}
          </p>
          <section className="account-panel">
            <h2>Download your data</h2>
            <p>
              Save a JSON copy of your vocabulary, lookup history, review
              schedule, practice, games, and learning preferences. Generated
              audio is listed as metadata.
            </p>
            <p>
              Exports can contain private text and page addresses. Store your
              download somewhere safe.
            </p>
            <button
              className="account-button"
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
            <p>
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
              className="account-button account-delete"
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
            <a href={API + "/auth/login?returnTo=/account"}>
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
