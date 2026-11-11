# Real-life conversation challenges

## Intent and scope

The user wants harder, more realistic everyday English in the existing 3D game.
Prior instructions delegate routine design decisions and request concise,
readable code, automatic voice checking, corrections and regressions. Keep the
Godot game, local Whisper/Qwen/Phonon, current art and controls. Do not add RAG,
cloud services, packages, a public deployment or paid assets.

The user selected US and Canada first. Use six US and five Canadian role
contexts, with currency identified in setting briefs and natural dollar wording
in speech. These are fictional individual situations, not claims about everyone
in a country. Accept natural regional vocabulary alternatives. UI stays English.

## Approach

A text-only difficulty increase is small but leaves the one-answer NPC loop.
Unbounded generated dialogue gives variety but weakens task/reward/save control.
Choose authored real-life challenges with AI review: the app owns the questions
and progress, while the model reviews open wording and grounded evidence.
This is an implementation choice under the user's standing delegation.

## Content and interaction

- Scene menu offers Everyday and Real-life challenge. Everyday role scenes and
  the original café order/friend journey retain their rules and saves.
- Challenge covers the existing five settings/two roles plus café/customer.
  Each has three goals, each with an opening question and one follow-up at the
  same NPC. Complete both before the action/reward and next NPC become available.
- Use original casual dialogue: requests, reasons, substitutions, budget/time
  constraints, polite disagreement, order repair and backup plans. Every exchange
  has two clear criteria and an example, with alternatives explicitly welcome.
- Six new café exchanges use Maya twice at the counter then Alex at the table;
  reuse the existing forest café through a small world adapter. Its menu/prices
  are fictional conversation context, not a replacement economic simulation.
- Primary answers are retained as follow-up context. A failed answer keeps that
  exchange; repeating its correction is assisted. A corrected opening still
  needs its follow-up. Any assisted exchange marks the goal assisted.
- Voice questions play automatically and stopping recording submits as before.
  Clear requests to repeat replay the current line without grading, advancing,
  appending history or replacing a pending correction.

## Contracts

`data/real_life.json` is a full challenge catalog: array of six scenes, each
{id,title,roles}. Roles are {id,label,npc,setting,turns}; the first five retain
both existing IDs, café has `customer`. Three turns each, with existing
{npc,prompt,goal,sample,action} plus `criteria` (exactly two strings) and
`follow_up` {prompt,goal,sample,criteria}. Prompts <=240 chars, samples/goals
<=240, each criterion <=160. NPC/action routes mirror the original five scenes;
café uses counter/counter/friend, Maya/Maya/Alex, actions basket/pay/table.

`/roleplay` keeps existing fields and adds optional `difficulty` (everyday or
challenge) and `exchange` (0 or 1). Old clients remain valid. Everyday history
length equals turn and exchange is zero; challenge length equals turn*2+exchange,
maximum five entries. Only challenge accepts café/customer. Model context selects
exactly the active exchange and treats current text/history as untrusted data.

Challenge review asks for one quoted evidence item per criterion (met + quote).
A bounded grammar offers at most eight exact current-reply evidence excerpts
(including the full reply), avoiding model copying errors without extra inference.
Accepted quotes must come from the current reply; history cannot stand in for a
missing answer. Reject bare yes/okay, missing evidence, fabricated quotes,
injection and help commands. Exact authored examples may resolve an inconsistent
model verdict. Keep useful learner-preserving grammar repairs, avoid sample-choice
hallucinations and unsupported pronunciation/accent/fluency claims. The public
output remains the existing four feedback fields. This improves grounded task
review; it is not a validated proficiency or phonetic assessment.

The lesson tracks difficulty, opening reply and whether either exchange was
assisted. Completed records retain opening/follow-up separately. Existing v1
saves migrate to Everyday without discarding progress. Challenge session keys
are scene/role/challenge; Everyday keys stay scene/role. Restore validates phase,
answer count and bounded strings. Switching/restarting cancels pending work as
before; restart touches only the selected difficulty. Original café saves stay
separate. Do not commit/push unrelated or current work in this task.

## Reference and original authorship

US Department of State's everyday speaking topics informed the US contexts:
https://americanenglish.state.gov/resources/everyday-conversations-learning-american-english
Toronto's visitor transport page supports the streetcar setting:
https://www.toronto.ca/explore-enjoy/visitor-toronto/getting-around-the-city/
British Council's B1 speaking overview informed communication functions:
https://learnenglish.britishcouncil.org/free-resources/speaking/b1
Write original prompts; do not copy their dialogues, videos or images. Prices,
stock, return rules and opening hours are stated fictional venue facts, not
national customs, actual business policies or legal advice.

## Acceptance

Regress multi-exchange advancement, assisted correction, requests to repeat,
history/phase validation, evidence grounding, mode isolation, old saves and stale
responses. Check real browser navigation, recorded submission, NPC follow-up,
completion and restoration with fixtures; smoke the local model on public sample
text. Export browser/macOS, verify native startup, and leave a playable challenge
open. Keep the known software-renderer performance limitation explicit.
