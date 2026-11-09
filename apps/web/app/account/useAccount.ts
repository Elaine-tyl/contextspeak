"use client";
import { useEffect, useState } from "react";
import { api, ApiError, type WebSession } from "../../src/api";
import {
  deleteAccount,
  downloadAccount,
  type DeletionResult,
} from "../../src/account";
export function useAccount() {
  const [session, setSession] = useState<WebSession | null>(null);
  const [checked, setChecked] = useState(false);
  const [confirmation, setConfirmation] = useState("");
  const [busy, setBusy] = useState<"export" | "delete" | null>(null);
  const [message, setMessage] = useState("");
  const [deleted, setDeleted] = useState<DeletionResult | null>(null);
  useEffect(() => {
    let active = true;
    void api<WebSession>("", "/auth/session")
      .then((value) => {
        if (active) setSession(value);
      })
      .catch(() => {})
      .finally(() => {
        if (active) setChecked(true);
      });
    return () => {
      active = false;
    };
  }, []);
  async function run(action: "export" | "delete") {
    if (!session || busy || (action === "delete" && confirmation !== "DELETE"))
      return;
    setBusy(action);
    setMessage("");
    try {
      if (action === "delete") {
        setDeleted(await deleteAccount(session.csrfToken, confirmation));
        setSession(null);
        setConfirmation("");
      } else {
        const blob = await downloadAccount(session.csrfToken);
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = "contextspeak-data.json";
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        setMessage("Your data export is ready. Check your downloads.");
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) setSession(null);
      setMessage(
        action === "delete"
          ? "Could not confirm the deletion result. Check your sign-in status before trying again."
          : error instanceof Error
            ? error.message
            : "Export could not be completed. Try again.",
      );
    } finally {
      setBusy(null);
    }
  }
  return {
    session,
    checked,
    confirmation,
    setConfirmation,
    busy,
    message,
    deleted,
    run,
  };
}
