import {
  BadRequestException,
  ConflictException,
  NotFoundException,
  ServiceUnavailableException,
} from "@nestjs/common";
import { createHash } from "node:crypto";
import type { PrismaClient, User } from "@prisma/client";
import {
  learningProfileSchema,
  scenarioOutputSchema,
  type Identity,
  type GoalSettings,
  type LearningProfile,
  type Personalization,
} from "@contextspeak/contracts";
import { LibraryService } from "../library/library.service";
import { frequencyFor, frequencySource } from "./frequency";
import { goalClues, rankPriority } from "./priority";
import type { ScenarioProvider } from "./scenario.provider";
const empty: LearningProfile = {
  version: 1,
  goals: [],
  dismissed: [],
  suggestions: [],
  analyzedContexts: [],
  analyzedAt: null,
  analyzedThrough: null,
  model: null,
};
function contextKey(context: string) {
  return createHash("sha256")
    .update(context.normalize("NFKC").trim().replace(/\s+/g, " ").toLowerCase())
    .digest("hex");
}
function profile(value: unknown): LearningProfile {
  const parsed = learningProfileSchema.safeParse(value);
  return parsed.success ? parsed.data : { ...empty };
}
export class PersonalizationService {
  private requests: number[] = [];
  private pending = new Map<string, Promise<LearningProfile>>();
  constructor(
    private db: PrismaClient,
    private library: LibraryService,
    private provider?: ScenarioProvider,
  ) {}
  private history(userId: string) {
    return this.db.lookupEvent.findMany({
      where: {
        userId,
        translationStatus: "succeeded",
        OR: [{ sourceLanguage: "en" }, { detectedSourceLanguage: "en" }],
      },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 500,
    });
  }
  private contexts(
    rows: Awaited<ReturnType<PersonalizationService["history"]>>,
  ) {
    const seen = new Set<string>();
    return rows.filter((row) => {
      const context =
        row.context?.normalize("NFKC").trim().replace(/\s+/g, " ") ?? "";
      const key = context.toLowerCase();
      if (context.length < 20 || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
  }
  async get(identity: Identity): Promise<Personalization> {
    const user = await this.library.actor(identity);
    return this.forUser(user);
  }
  async forUser(user: User): Promise<Personalization> {
    const current = profile(user.learningProfile);
    const rows = await this.history(user.id);
    const [items, attempts] = await Promise.all([
      this.db.learningItem.findMany({
        where: {
          userId: user.id,
          id: { in: [...new Set(rows.map((row) => row.candidateId))] },
        },
        include: { reviewCard: true },
      }),
      this.db.practiceAttempt.findMany({
        where: {
          userId: user.id,
          createdAt: { gte: new Date(Date.now() - 30 * 86400000) },
        },
        include: { lookup: { select: { candidateId: true } } },
        orderBy: [{ createdAt: "desc" }, { id: "desc" }],
        take: 500,
      }),
    ]);
    const priorities = items
      .map((item) => {
        const encounters = rows.filter((row) => row.candidateId === item.id);
        const latest = encounters[0];
        const clues = goalClues(
          current.goals,
          encounters
            .map((row) => row.text + " " + (row.context ?? ""))
            .join(" "),
        );
        const scores = new Map<string, number>();
        for (const attempt of attempts.filter(
          (row) => row.lookup.candidateId === item.id,
        )) {
          const task = attempt.task as {
            independent?: boolean;
            exerciseKey?: string;
            feedback?: { score?: number };
            speakingAssessment?: {
              rubricVersion?: number;
              audioQuality?: string;
              overall?: number | null;
            };
          };
          if (
            !task.independent ||
            !task.exerciseKey ||
            scores.has(task.exerciseKey)
          )
            continue;
          const assessment = task.speakingAssessment;
          const score = assessment
            ? assessment.rubricVersion === 2 &&
              assessment.audioQuality === "clear"
              ? assessment.overall
              : null
            : task.feedback?.score;
          if (
            typeof score === "number" &&
            Number.isFinite(score) &&
            score >= 0 &&
            score <= 100
          )
            scores.set(task.exerciseKey, score);
        }
        const frequency = frequencyFor(item.text);
        const due = Boolean(
          item.reviewCard?.active && item.reviewCard.dueAt <= new Date(),
        );
        return {
          id: item.id,
          lookupId: latest.id,
          word: item.text,
          targetLanguage: item.targetLanguage,
          encounters: encounters.length,
          ...rankPriority({
            encounters: encounters.length,
            goal: current.goals.length ? (clues.length ? 1 : 0) : null,
            frequency,
            gap:
              scores.size >= 2
                ? 1 -
                  [...scores.values()].reduce((a, b) => a + b, 0) /
                    scores.size /
                    100
                : null,
            due,
          }),
          goalClues: clues,
          frequency,
          practiceEvidence: scores.size,
          due,
        };
      })
      .sort(
        (a, b) =>
          b.score - a.score ||
          a.word.localeCompare(b.word) ||
          a.id.localeCompare(b.id),
      )
      .slice(0, 20);
    const evidenceIds = current.suggestions.flatMap(
      (value) => value.evidenceIds,
    );
    const evidence = await this.db.lookupEvent.findMany({
      where: { userId: user.id, id: { in: evidenceIds } },
      select: { id: true, text: true, context: true },
    });
    return {
      profile: current,
      contextCount: this.contexts(rows).length,
      historySampleSize: rows.length,
      priorities,
      frequencySource,
      evidence,
      suggestionAvailable: Boolean(this.provider),
    };
  }
  async goals(identity: Identity, settings: GoalSettings) {
    const user = await this.library.actor(identity);
    return this.db.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id=${user.id} FOR UPDATE`;
      const current = await tx.user.findUnique({ where: { id: user.id } });
      if (!current) throw new NotFoundException();
      const value = {
        ...profile(current.learningProfile),
        goals: [...new Set(settings.goals)],
        dismissed: [
          ...new Set(
            settings.dismissed ?? profile(current.learningProfile).dismissed,
          ),
        ],
      };
      await tx.user.update({
        where: { id: user.id },
        data: { learningProfile: value },
      });
      return value;
    });
  }
  async suggest(identity: Identity) {
    const user = await this.library.actor(identity);
    const active = this.pending.get(user.id);
    if (active) return active;
    const work = this.analyze(user);
    this.pending.set(user.id, work);
    try {
      return await work;
    } finally {
      this.pending.delete(user.id);
    }
  }
  private async analyze(user: User) {
    const current = profile(user.learningProfile);
    const rows = this.contexts(await this.history(user.id));
    const hashes = rows.map((row) => contextKey(row.context!));
    const through = createHash("sha256")
      .update(JSON.stringify([...hashes].sort()))
      .digest("hex");
    if (current.analyzedThrough === through && current.analyzedAt)
      return current;
    // Use the captured context set, not provider completion time: lookups made
    // during a slow model call must remain eligible for the next analysis.
    const previous = new Set(current.analyzedContexts);
    const fresh = rows.filter((row) => !previous.has(contextKey(row.context!)));
    if (fresh.length < 10)
      throw new BadRequestException(
        "Save ten different new English contexts before requesting suggestions.",
      );
    if (!this.provider)
      throw new ServiceUnavailableException(
        "Scenario suggestions are unavailable. Choose goals yourself.",
      );
    this.requests = this.requests.filter((time) => time > Date.now() - 60000);
    if (this.pending.size > 2 || this.requests.length >= 6)
      throw new ServiceUnavailableException(
        "Suggestions are busy. Try again later.",
      );
    this.requests.push(Date.now());
    const batch = fresh.slice(0, 30).map((row) => ({
      id: row.id,
      text: row.text.slice(0, 200),
      context: row.context!.slice(0, 500),
    }));
    let result;
    try {
      result = await this.provider.suggest(batch);
      const parsed = scenarioOutputSchema.parse({
        suggestions: result.suggestions,
      });
      if (
        parsed.suggestions.some((value) =>
          value.evidenceIds.some((id) => !batch.some((row) => row.id === id)),
        )
      )
        throw new Error("Unsupported evidence");
      result = { ...parsed, model: result.model };
    } catch {
      throw new ServiceUnavailableException(
        "Suggestions could not be checked. Your goals are unchanged.",
      );
    }
    return this.db.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id=${user.id} FOR UPDATE`;
      const latest = await tx.user.findUnique({ where: { id: user.id } });
      if (!latest) throw new NotFoundException();
      if (latest.consentVersion !== user.consentVersion)
        throw new ConflictException(
          "Collection settings changed. Suggestions discarded.",
        );
      const value = {
        ...profile(latest.learningProfile),
        suggestions: [
          ...new Map(
            result.suggestions.map((value) => [value.scenario, value]),
          ).values(),
        ],
        analyzedContexts: hashes,
        analyzedAt: new Date().toISOString(),
        analyzedThrough: through,
        model: result.model,
      };
      await tx.user.update({
        where: { id: user.id },
        data: { learningProfile: value },
      });
      return value;
    });
  }
}
