import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';

const script = readFileSync(new URL('../../games/unity/Assets/WebGLTemplates/ContextSpeak/pointer-lock.js', import.meta.url), 'utf8');

function install(request, focused = true) {
  const canvas = { isConnected: true, requestPointerLock: request };
  const document = { hasFocus: () => focused, getElementById: () => canvas };
  runInNewContext(script, { document });
  return canvas;
}

test('focus loss does not request pointer lock against an inactive document', () => {
  let calls = 0;
  install(() => calls++, false).requestPointerLock();
  assert.equal(calls, 0);
});

test('expected browser denials do not become fatal Unity errors', async () => {
  for (const name of ['WrongDocumentError', 'NotAllowedError', 'SecurityError']) {
    const canvas = install(() => Promise.reject(Object.assign(new Error('Denied'), { name })));
    await assert.doesNotReject(() => canvas.requestPointerLock());
  }
});

test('successful lock requests and unexpected failures remain observable', async () => {
  const canvas = install(() => Promise.resolve('locked'));
  assert.equal(await canvas.requestPointerLock(), 'locked');
  const broken = install(() => Promise.reject(new TypeError('Unexpected failure')));
  await assert.rejects(() => broken.requestPointerLock(), /Unexpected failure/);
});
