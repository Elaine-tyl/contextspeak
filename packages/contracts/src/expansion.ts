import { z } from "zod";

const related = z
  .object({
    term: z.string().trim().min(1).max(40),
    difference: z.string().trim().min(1).max(350),
    example: z
      .object({
        source: z.string().min(1).max(500),
        translation: z.string().min(1).max(500),
      })
      .strict(),
  })
  .strict();
export const expansionOutputSchema = z
  .object({
    synonyms: z.array(related).max(2),
    lookalikes: z.array(related).max(2),
  })
  .strict();
export const wordExpansionSchema = expansionOutputSchema.extend({
  model: z.string().min(1).max(200),
});
export type WordExpansion = z.infer<typeof wordExpansionSchema>;
