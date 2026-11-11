mergeInto(LibraryManager.library, {
  CS_MicStart: function (owner, generation) {
    window.ContextSpeakUnity.record(UTF8ToString(owner), generation);
  },
  CS_MicStop: function () { window.contextSpeakMicrophone.stop(); },
  CS_MicCancel: function () { window.contextSpeakMicrophone.cancel(); },
  CS_State: function (json) { window.ContextSpeakUnity.state(UTF8ToString(json)); }
});
