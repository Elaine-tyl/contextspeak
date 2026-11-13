import {
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
} from "react";

export function LetterWord({
  value,
  length,
  label,
  inputRef,
  onChange,
  onComplete,
  onKeyDown,
}: {
  value: string;
  length: number;
  label: string;
  inputRef(input: HTMLInputElement | null): void;
  onChange(value: string): void;
  onComplete(): void;
  onKeyDown(event: KeyboardEvent<HTMLInputElement>): void;
}) {
  const input = useRef<HTMLInputElement | null>(null);
  const [focused, setFocused] = useState(false);
  const [cursor, setCursor] = useState(0);
  return (
    <span
      className="gap-word"
      style={{ "--letter-count": length } as CSSProperties}
    >
      {/* One native input keeps paste, selection, mobile keyboards and IME working. */}
      <input
        ref={(element) => {
          input.current = element;
          inputRef(element);
        }}
        className="letter-input"
        aria-label={label}
        aria-describedby="gap-typing-hint"
        value={value}
        maxLength={length}
        autoComplete="off"
        autoCapitalize="none"
        spellCheck={false}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onSelect={(event) => setCursor(event.currentTarget.selectionStart ?? 0)}
        onKeyDown={onKeyDown}
        onChange={(event) => {
          const next = event.target.value.slice(0, length);
          onChange(next);
          if (
            !(event.nativeEvent as InputEvent).isComposing &&
            next.length === length &&
            next.length > value.length
          )
            onComplete();
        }}
      />
      <span className="letter-cells" aria-hidden="true">
        {Array.from({ length }, (_, index) => (
          <span
            key={index}
            className={`letter-cell ${focused && index === Math.min(cursor, length - 1) ? "is-current" : ""} ${value[index] ? "is-filled" : ""}`}
            onPointerDown={(event) => {
              event.preventDefault();
              input.current?.focus();
              input.current?.setSelectionRange(index, index + 1);
              setCursor(index);
            }}
          >
            {value[index] ?? ""}
          </span>
        ))}
      </span>
    </span>
  );
}
