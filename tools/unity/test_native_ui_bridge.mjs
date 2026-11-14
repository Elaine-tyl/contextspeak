import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';

const template = new URL('../../games/unity/Assets/WebGLTemplates/ContextSpeak/', import.meta.url);
const script = readFileSync(new URL('cafe.js', template), 'utf8');
const css = readFileSync(new URL('cafe.css', template), 'utf8');
function install() {
  const classes = new Set();
  const nodes = new Map();
  const node = (id) => {
    if (!nodes.has(id)) nodes.set(id, { value: '', addEventListener() {}, replaceChildren() {}, append() {} });
    return nodes.get(id);
  };
  const document = {
    body: { classList: { toggle(name, on) { if (on) classes.add(name); else classes.delete(name); } } },
    getElementById: node, addEventListener() {}, createElement: node,
  };
  const window = { addEventListener() {}, ContextSpeakCharacter: { render() {} } };
  runInNewContext(script, { document, window, URL, Option: function () {}, location: { href: 'http://localhost/unity/' }, history: { replaceState() {} } });
  return { bridge: window.ContextSpeakUnity, classes, window };
}
test('native gameplay hides duplicate DOM overlays while retaining the character bridge', () => {
  const { bridge, classes } = install();
  for (const mode of ['cafe', 'life', 'slice']) {
    bridge.state(JSON.stringify({ mode, recording: 'idle' }));
    assert.ok(classes.has('native-unity-ui'), mode);
  }
  bridge.state(JSON.stringify({ mode: 'character' }));
  assert.ok(!classes.has('native-unity-ui'));
  assert.ok(classes.has('character-creation'));
  for (const id of ['mission', 'life-menu', 'dialogue', 'interact', 'travel-status'])
    assert.ok(css.includes('.native-unity-ui #' + id), id);
});
test('native presentation retains microphone recording callbacks to the owning Unity object', () => {
  const { bridge, window } = install();
  const messages = [];
  bridge.instance = { SendMessage: (...args) => messages.push(args) };
  window.contextSpeakMicrophone = { start(ready) { ready(); } };
  bridge.record('ForestCafeSlice', 7);
  assert.equal(messages[0][0], 'ForestCafeSlice');
  assert.equal(messages[0][1], 'OnMicReady');
  assert.deepEqual(JSON.parse(messages[0][2]), { generation: 7, value: '' });
});

test('legacy locations own native UI even when they publish no browser state', () => {
  const { bridge, classes } = install();
  const instance = {};
  bridge.ready(instance);
  assert.equal(bridge.instance, instance);
  assert.ok(classes.has('native-unity-ui'));
  bridge.state(JSON.stringify({ mode: 'character' }));
  bridge.ready(instance);
  assert.ok(!classes.has('native-unity-ui'), 'Character HTML must stay available');
});

test('voice meter keeps the recording owner and generation without uploading more audio', () => {
  const { bridge, window } = install();
  const messages = [];
  bridge.instance = { SendMessage: (...args) => messages.push(args) };
  window.contextSpeakMicrophone = { start(ready, activity) { ready(); activity(.65); } };
  bridge.record('VoiceOnlyCoach', 12);
  assert.equal(messages[1][0], 'VoiceOnlyCoach');
  assert.equal(messages[1][1], 'OnMicActivity');
  assert.deepEqual(JSON.parse(messages[1][2]), { generation: 12, level: .65 });
});
