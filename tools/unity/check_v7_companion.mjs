// Optional live smoke test: only public authored text, local companion, no account or cloud key.
import { writeFile } from 'node:fs/promises';
const origin = 'http://127.0.0.1:4320';
const session = await fetch(origin + '/session', { headers: { 'X-ContextSpeak-Client': 'native' } });
if (!session.ok) throw new Error('Start pnpm world:dev before this live check.');
const { token } = await session.json();
const cases = [
  { name: 'cafe-correction', band: 'IELTS 5.0–5.5', npc: 'Maya', task: 'café', npcLine: 'What would you like?', goal: 'Make a clear request for a drink.', text: 'Can I has a latte?', reference: '', history: [] },
  { name: 'school-clarification', band: 'IELTS 5.0–5.5', npc: 'Ms Taylor', task: 'school', npcLine: 'You missed part of the homework instructions. Ask me to clarify them and explain what you understood.', goal: 'Ask for clarification and explain what was understood.', text: 'Could you repeat the homework instructions? I understood that we need to write a paragraph, but I missed the deadline.', reference: '', history: ['Teacher: Write about a place you like.'] },
  { name: 'home-request', band: 'IELTS 5.0–5.5', npc: 'Alex', task: 'home', npcLine: 'How can we share the cooking tonight?', goal: 'Suggest a fair way to divide the work.', text: 'Could you wash the vegetables while I cook the pasta? That would help us finish dinner together.', reference: '', history: [] },
];
cases.push(
  { name: 'repeat-alternative', band: 'IELTS 5.0–5.5', npc: 'Maya', task: 'café', npcLine: 'What would you like?', goal: 'Request a drink and preserve the reference meaning.', text: 'Could I get a latte, please?', reference: 'Can I have a latte?', history: [] },
  { name: 'repeat-wrong-meaning', expectedGoal: false, band: 'IELTS 5.0–5.5', npc: 'Maya', task: 'café', npcLine: 'What would you like?', goal: 'Request a drink and preserve the reference meaning.', text: 'I would like a tea, please.', reference: 'Can I have a latte?', history: [] },
);
const results = [];
for (const { name, expectedGoal = true, ...body } of cases) {
  const response = await fetch(origin + '/slice/review', {
    method: 'POST', headers: { 'X-ContextSpeak-Client': 'native', Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
    body: JSON.stringify(body), signal: AbortSignal.timeout(90000),
  });
  const value = await response.json();
  const passed = response.ok && value.assessedGoal === true && value.goalMet === expectedGoal
    && (name !== 'cafe-correction' || Boolean(value.correction));
  const result = { name, passed, status: response.status, assessed: value.assessedGoal, goalMet: value.goalMet, correction: value.correction };
  results.push(result); console.log(JSON.stringify(result));
}
await writeFile('.local/v7/live-companion.json', JSON.stringify(results, null, 2));
if (results.some(result => !result.passed)) process.exitCode = 1;
