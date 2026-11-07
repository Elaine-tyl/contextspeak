# ContextSpeak

**Build your vocabulary. Understand conversations. Speak with confidence.**

A browser extension and learning website that turns everyday English lookups into a personal vocabulary memory library, scheduled review, and contextual listening/speaking practice. The first release serves adults with basic English; broader language and school support follow later.

> Planning stage. No application has been implemented yet. Scope and provider choices below are implementation proposals.

## System overview

![ContextSpeak: automatic lookup history, vocabulary memory, inferred scenarios, review, and listening/speaking practice](docs/assets/conceptual-diagram.png)

[Editable SVG](docs/assets/conceptual-diagram.svg)

## Documentation

| Document | Purpose |
| --- | --- |
| [Planning](docs/PLANNING.md) | Requirements mapping, MVP, roadmap, costs, and decisions |
| [Methodology](docs/METHODOLOGY.md) | Collection, scenario inference, word priorities, and mastery evidence |
| [Architecture](docs/ARCHITECTURE.md) | Stack, translation/dictionary services, data flow, and safeguards |
| [First coding plan](docs/superpowers/plans/2026-11-07-capture-to-library.md) | A bounded capture-to-library implementation with checks |

## Working principles

- After collection consent, automatically record each selection-translation query and derive vocabulary candidates; activation for scheduled review requires confirmation.
- Combine learner goals, inferred scenarios, and separate reading/listening/speaking evidence.
- Build independently. [FluentRead](https://github.com/FluentRead/FluentRead) is a functional reference, not a source of copied code or assets. This is not a legal clearance statement.

A distribution license is undecided. The diagram adapts a user-supplied layout reference; its ownership/permission and the ContextSpeak brand still need clearance before public release.
