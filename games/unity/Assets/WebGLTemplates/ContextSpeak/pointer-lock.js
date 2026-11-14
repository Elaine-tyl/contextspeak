// Browser focus can change while Unity requests mouse look. A normal denial must not crash the game.
(() => {
  const canvas = document.getElementById("unity-canvas");
  if (!canvas?.requestPointerLock) return;
  const request = canvas.requestPointerLock.bind(canvas);
  const expected = new Set([
    "WrongDocumentError",
    "NotAllowedError",
    "SecurityError",
  ]);
  const handle = (error) => {
    if (!expected.has(error.name)) throw error;
  };
  canvas.requestPointerLock = (...args) => {
    if (!canvas.isConnected || !document.hasFocus()) return;
    try {
      const result = request(...args);
      return result?.catch ? result.catch(handle) : result;
    } catch (error) {
      return handle(error);
    }
  };
})();
