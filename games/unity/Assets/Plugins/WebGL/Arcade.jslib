mergeInto(LibraryManager.library, {
  CS_ArcadeReady: function () { window.ContextSpeakArcade.emit("ready", "", ""); },
  CS_ArcadeAction: function (action, answer, roundId) {
    window.ContextSpeakArcade.emit(UTF8ToString(action), UTF8ToString(answer), UTF8ToString(roundId));
  },
  CS_ArcadeReport: function (json) { window.ContextSpeakArcade.report(UTF8ToString(json)); }
});
