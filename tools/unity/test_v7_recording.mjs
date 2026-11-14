import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';
const worklet = readFileSync(new URL('../../games/cafe/web/capture-worklet.js', import.meta.url), 'utf8');
function capture(maxSeconds) {
  let Processor;
  const messages = [];
  runInNewContext(worklet, { sampleRate: 100, Float32Array,
    AudioWorkletProcessor: class { constructor() { this.port = { postMessage(value) { messages.push(value); } }; } },
    registerProcessor(_name, value) { Processor = value; },
  });
  const processor = new Processor({ processorOptions: { maxSeconds } });
  return { processor, messages };
}
test('exam capture stays active past twenty seconds and respects the two-minute bound', () => {
  const { processor, messages } = capture(120);
  for (let i = 0; i < 21; i++) assert.equal(processor.process([[new Float32Array(100)]]), true);
  assert.equal(messages.some(message => message.done), false);
  for (let i = 21; i < 119; i++) processor.process([[new Float32Array(100)]]);
  assert.equal(processor.process([[new Float32Array(100)]]), false);
  assert.equal(messages.filter(message => message.done).length, 1);
});
test('normal conversations keep their existing short capture limit', () => {
  const { processor } = capture(undefined);
  for (let i = 0; i < 19; i++) assert.equal(processor.process([[new Float32Array(100)]]), true);
  assert.equal(processor.process([[new Float32Array(100)]]), false);
});
