"use client";
import { useEffect, useRef, useState } from "react";
import type { LearningProgress } from "@contextspeak/contracts";
import { api, ApiError, type WebSession } from "../../src/api";
export function useProgress() {
  const [data, setData] = useState<LearningProgress | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [token, setToken] = useState("");
  const [signedIn, setSignedIn] = useState(false);
  const refresh = useRef(() => {});
  useEffect(() => {
    const controller = new AbortController();
    let owner = "",
      blocked = false,
      pending = false,
      queued = false,
      failed = false,
      last = 0;
    let expiry: ReturnType<typeof setTimeout>;
    function expire() {
      blocked = true;
      owner = "";
      setData(null);
      setSignedIn(false);
      setToken("");
      setError("Sign in to see your learning progress.");
    }
    async function load(force = false) {
      if (blocked) return;
      if (pending) {
        if (force) queued = true;
        return;
      }
      pending = true;
      try {
        const session = await api<WebSession>(
          "",
          "/auth/session",
          "GET",
          undefined,
          { signal: controller.signal },
        );
        if (controller.signal.aborted) return;
        const remaining = Date.parse(session.expiresAt) - Date.now();
        if (remaining <= 0 || (owner && owner !== session.csrfToken)) {
          expire();
          return;
        }
        owner = session.csrfToken;
        setToken(owner);
        setSignedIn(true);
        clearTimeout(expiry);
        expiry = setTimeout(expire, Math.min(remaining, 2147483647));
        if (force || (!failed && Date.now() - last >= 30000)) {
          const value = await api<LearningProgress>(
            owner,
            "/v1/progress",
            "GET",
            undefined,
            { signal: controller.signal },
          );
          if (controller.signal.aborted || blocked) return;
          setData(value);
          setError("");
          last = Date.now();
          failed = false;
        }
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && [401, 403].includes(error.status))
          expire();
        else {
          failed = true;
          setError(
            "Could not refresh progress. Previously loaded results are kept; try Refresh.",
          );
        }
      } finally {
        pending = false;
        if (!controller.signal.aborted) {
          setLoading(false);
          if (queued) {
            queued = false;
            void load(true);
          }
        }
      }
    }
    refresh.current = () => void load(true);
    const returned = () => {
      if (document.visibilityState === "visible") void load(true);
    };
    void load();
    const timer = setInterval(() => void load(), 3000);
    window.addEventListener("focus", returned);
    document.addEventListener("visibilitychange", returned);
    return () => {
      blocked = true;
      controller.abort();
      clearInterval(timer);
      clearTimeout(expiry);
      window.removeEventListener("focus", returned);
      document.removeEventListener("visibilitychange", returned);
      refresh.current = () => {};
    };
  }, []);
  return {
    data,
    error,
    loading,
    signedIn,
    token,
    refresh: () => refresh.current(),
  };
}
