import type { Personalization } from "./personalization";
import type { GameKind, GameTrack } from "./games";
export type SkillProgress = {
  scored: number;
  average: number | null;
  unscorable: number;
  focus: { dimension: string; evidenceCount: number } | null;
};
export type LearningProgress = {
  personalization?: Personalization;
  updatedAt: string;
  since: string;
  sampleLimit: number;
  vocabulary: {
    saved: number;
    active: number;
    paused: number;
    due: number;
    reviews: number;
    recentRatings: Record<"again" | "hard" | "good" | "easy", number>;
    difficult: { id: string; word: string; count: number }[];
  };
  listening: SkillProgress;
  speaking: SkillProgress;
  games: {
    kind: GameKind;
    label: string;
    track: GameTrack;
    level: number;
  }[];
};
