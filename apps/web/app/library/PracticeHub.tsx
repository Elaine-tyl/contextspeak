"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import {
  aiLanguages,
  conversationScenarios,
  type PracticeItem,
} from "@contextspeak/contracts";
import { api } from "../../src/api";
import { withLibraryReturn } from "../../src/navigation";
import {
  rememberLibraryScroll,
  updateQuery,
  usePageQuery,
  useRestoreLibraryScroll,
} from "../../src/page-query";

export function PracticeHub({ token }: { token: string }) {
  const { search, params } = usePageQuery();
  const scenario =
    conversationScenarios.find(
      (value) => value.id === params.get("practiceScenario"),
    )?.id ?? "";
  const query = (params.get("practiceQ") ?? "").slice(0, 200);
  const language = params.get("practiceLanguage") ?? "";
  const returnTo = "/library" + search;
  const [items, setItems] = useState<PracticeItem[]>([]);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  useRestoreLibraryScroll(returnTo, !loading && items.length > 0);
  useEffect(() => {
    const controller = new AbortController();
    setItems([]);
    setMessage("");
    setLoading(true);
    const timer = setTimeout(async () => {
      const params = new URLSearchParams({
        q: query,
        targetLanguage: language,
      });
      if (!language) params.delete("targetLanguage");
      try {
        const result = await api<{ items: PracticeItem[] }>(
          token,
          "/v1/practice?" + params,
          "GET",
          undefined,
          { signal: controller.signal },
        );
        if (!controller.signal.aborted) setItems(result.items);
      } catch (error) {
        if (!controller.signal.aborted)
          setMessage(
            error instanceof Error ? error.message : "Could not load practice.",
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }, 200);
    // Discard old searches and account responses when navigating away.
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [token, query, language, retry]);
  return (
    <section className="library-section">
      <h2>Listening &amp; speaking</h2>
      <p className="section-copy">
        Practice with your saved vocabulary and AI audio. Type what you hear and
        submit for marks and corrections, or record and replay a spoken
        response. Results stay separate from vocabulary recall.
      </p>
      <div
        className="library-filters"
        role="search"
        aria-label="Filter practice"
      >
        <label>
          Search
          <input
            type="search"
            aria-label="Search practice"
            maxLength={200}
            value={query}
            onChange={(event) => updateQuery({ practiceQ: event.target.value })}
          />
        </label>
        <label>
          Conversation scenario
          <select
            value={scenario}
            onChange={(event) =>
              updateQuery({ practiceScenario: event.target.value })
            }
          >
            <option value="">Saved example</option>
            {conversationScenarios.map((value) => (
              <option key={value.id} value={value.id}>
                {value.title}
              </option>
            ))}
          </select>
        </label>
        <label>
          Translation language
          <select
            value={language}
            onChange={(event) =>
              updateQuery({ practiceLanguage: event.target.value })
            }
          >
            <option value="">All languages</option>
            {aiLanguages.map((item) => (
              <option key={item.code} value={item.code}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
        <button
          className="secondary"
          onClick={() => setRetry((value) => value + 1)}
        >
          Refresh practice
        </button>
      </div>
      {scenario && (
        <p className="section-copy">
          English role-play: choose saved English vocabulary to use in your
          reply.
        </p>
      )}
      {message ? (
        <p role="alert">{message}</p>
      ) : loading ? (
        <p role="status">Loading practice…</p>
      ) : !items.length ? (
        <p>
          No matching practice yet. Save a successful translation to get
          started.
        </p>
      ) : (
        <>
          <p className="section-copy">
            Up to 50 recent matches. Search to find older vocabulary.
          </p>
          <div className="cards">
            {items.map((item) => (
              <article className="lookup-card" key={item.id}>
                <h3>
                  <Link
                    href={withLibraryReturn(
                      `/practice/${item.id}?lookup=${item.lookupId}${scenario ? `&scenario=${scenario}` : ""}`,
                      returnTo,
                    )}
                    onClick={() => rememberLibraryScroll()}
                  >
                    {item.text}
                  </Link>
                </h3>
                <p>
                  {aiLanguages.find(
                    (value) => value.code === item.sourceLanguage,
                  )?.name ?? "Auto-detected"}{" "}
                  →{" "}
                  {aiLanguages.find(
                    (value) => value.code === item.targetLanguage,
                  )?.name ?? item.targetLanguage}
                </p>
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
