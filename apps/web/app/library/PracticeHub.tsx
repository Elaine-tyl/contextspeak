"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import {
  aiLanguages,
  conversationScenarios,
  type PracticeItem,
} from "@contextspeak/contracts";
import { api } from "../../src/api";
import { withLibraryReturn, practiceMode } from "../../src/navigation";
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
  const mode = practiceMode(params.get("practiceMode"));
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
      <ol className="how-it-works" aria-label="How practice works">
        <li>
          <strong>Choose a word</strong>
          <span>Pick one of your saved words below.</span>
        </li>
        <li>
          <strong>Listen</strong>
          <span>Hear it in a sentence and type what you hear.</span>
        </li>
        <li>
          <strong>Speak</strong>
          <span>Answer a question out loud and get feedback.</span>
        </li>
      </ol>
      <div
        className="library-filters"
        role="search"
        aria-label="Filter practice"
      >
        <label className="filter-search">
          <span className="sr-only">Search</span>
          <input
            type="search"
            aria-label="Search practice"
            placeholder="Search your saved words"
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
            <option value="">Sentence I saved</option>
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
          Refresh
        </button>
      </div>
      {scenario && (
        <p className="section-copy">
          Role-play: pick a saved English word to use in a short conversation.
        </p>
      )}
      {message ? (
        <p role="alert">{message}</p>
      ) : loading ? (
        <p role="status">Loading practice…</p>
      ) : !items.length ? (
        <div className="empty">
          <h3>Nothing to practise yet</h3>
          <p>
            No matching practice yet. Look up a word with the extension, and
            once it is translated it will appear here.
          </p>
        </div>
      ) : (
        <>
          <p className="section-copy">
            Showing your 50 most recent words. Search to find older ones.
          </p>
          <div className="practice-grid">
            {items.map((item) => (
              <article
                className="lookup-card practice-item"
                key={item.id}
                data-depth
              >
                <h3>
                  <Link
                    href={withLibraryReturn(
                      `/practice/${item.id}?lookup=${item.lookupId}${mode === "speaking" ? "&mode=speaking" : ""}${scenario ? `&scenario=${scenario}` : ""}`,
                      returnTo,
                    )}
                    onClick={() => rememberLibraryScroll()}
                  >
                    {item.text}
                  </Link>
                </h3>
                <p className="candidate-meta">
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
