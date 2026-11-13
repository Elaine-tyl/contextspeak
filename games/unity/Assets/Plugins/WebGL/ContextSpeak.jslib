mergeInto(LibraryManager.library, {
  CS_MicStart: function (owner, generation) {
    window.ContextSpeakUnity.record(UTF8ToString(owner), generation);
  },
  CS_MicStop: function () { window.contextSpeakMicrophone.stop(); },
  CS_MicCancel: function () { window.contextSpeakMicrophone.cancel(); },
  CS_State: function (json) { window.ContextSpeakUnity.state(UTF8ToString(json)); },
  CS_SyncSave__deps: ["$FS"],
  CS_SyncSave: function (owner, generation) {
    var target = UTF8ToString(owner);
    function done(error) {
      if (window.ContextSpeakUnity.instance)
        window.ContextSpeakUnity.instance.SendMessage(target, "OnSaveSynced",
          JSON.stringify({ generation: generation, error: error ? "Browser storage is unavailable. Keep this page open and try Save again." : "" }));
    }
    try { FS.syncfs(false, done); }
    catch (error) { done(error); }
  }
});
