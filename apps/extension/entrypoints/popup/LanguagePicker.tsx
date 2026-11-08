import { useEffect, useId, useRef, useState } from "react";
import {
  CheckIcon,
  ChevronDownIcon,
  GlobeIcon,
  MagnifyingGlassIcon,
} from "@radix-ui/react-icons";

type Choice = { code: string; name: string };
export function LanguagePicker({
  label,
  value,
  choices,
  disabled,
  onChange,
}: {
  label: string;
  value: string;
  choices: Choice[];
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  const id = useId();
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const search = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(-1);
  const filtered = choices.filter((choice) =>
    `${choice.name} ${choice.code}`
      .toLowerCase()
      .includes(query.trim().toLowerCase()),
  );
  function close(restoreFocus = false) {
    setOpen(false);
    setQuery("");
    setActive(-1);
    if (restoreFocus) trigger.current?.focus();
  }
  function choose(code: string) {
    onChange(code);
    close(true);
  }
  useEffect(() => {
    if (open) search.current?.focus();
  }, [open]);
  // Close stale menus if account changes disable the controls mid-request.
  useEffect(() => {
    if (disabled) close();
  }, [disabled]);
  useEffect(() => {
    function outside(event: PointerEvent) {
      if (!root.current?.contains(event.target as Node)) close();
    }
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, []);
  return (
    <div
      className="language-picker"
      ref={root}
      onKeyDown={(event) => {
        if (event.key === "Escape" && open) {
          event.preventDefault();
          close(true);
        }
      }}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node)) close();
      }}
    >
      <span className="field-label" id={`${id}-label`}>
        {label}
      </span>
      <button
        type="button"
        className="language-trigger"
        ref={trigger}
        role="combobox"
        aria-labelledby={`${id}-label`}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-controls={`${id}-list`}
        data-value={value}
        disabled={disabled}
        onClick={() => (open ? close() : setOpen(true))}
        onKeyDown={(event) => {
          if (["ArrowDown", "ArrowUp"].includes(event.key)) {
            event.preventDefault();
            setOpen(true);
          }
          if (event.key === "Escape") close();
        }}
      >
        <GlobeIcon aria-hidden="true" />
        <span>
          {choices.find((choice) => choice.code === value)?.name ??
            "Choose language"}
        </span>
        <ChevronDownIcon aria-hidden="true" />
      </button>
      {open && !disabled && (
        <div className="language-menu">
          <div className="language-search">
            <MagnifyingGlassIcon aria-hidden="true" />
            <input
              type="search"
              ref={search}
              aria-label={`Search ${label}`}
              placeholder="Search languages"
              value={query}
              aria-controls={`${id}-list`}
              aria-activedescendant={
                active >= 0 && filtered[active]
                  ? `${id}-option-${active}`
                  : undefined
              }
              onChange={(event) => {
                setQuery(event.target.value);
                setActive(-1);
              }}
              onKeyDown={(event) => {
                if (event.key === "Escape") {
                  event.preventDefault();
                  close(true);
                }
                if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                  event.preventDefault();
                  const delta = event.key === "ArrowDown" ? 1 : -1;
                  setActive((previous) =>
                    filtered.length
                      ? previous < 0
                        ? delta > 0
                          ? 0
                          : filtered.length - 1
                        : (previous + delta + filtered.length) % filtered.length
                      : -1,
                  );
                }
                if (event.key === "Enter") {
                  event.preventDefault();
                  const choice = filtered[active >= 0 ? active : 0];
                  if (choice) choose(choice.code);
                }
              }}
            />
          </div>
          <div role="listbox" aria-label={`${label} options`} id={`${id}-list`}>
            {filtered.map((choice, index) => (
              <button
                type="button"
                role="option"
                tabIndex={-1}
                id={`${id}-option-${index}`}
                key={choice.code}
                aria-selected={choice.code === value}
                className={
                  index === active
                    ? "language-option active"
                    : "language-option"
                }
                onClick={() => choose(choice.code)}
              >
                <span>{choice.name}</span>
                {choice.code === value && <CheckIcon aria-hidden="true" />}
              </button>
            ))}
          </div>
          {!filtered.length && (
            <p className="empty-languages" role="status">
              No matching languages
            </p>
          )}
          <p className="menu-note">
            Only installed translation languages are available.
          </p>
        </div>
      )}
    </div>
  );
}
