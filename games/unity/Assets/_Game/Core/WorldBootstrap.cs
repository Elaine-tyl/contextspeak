using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe
{
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var query = Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var url) ? url.Query : "";
            var address = WorldSceneCatalog.Resolve(query, Application.platform == RuntimePlatform.WebGLPlayer);
            CharacterCreationController.ReturnWorld = address;
            if (!CharacterProfiles.Exists) address = "CharacterCreation";
            var root = CafeInterface.CreateRoot(gameObject);
            root.style.display = DisplayStyle.Flex;
            var status = new Label("ContextSpeak · Loading your neighborhood…");
            root.Add(status);
            var retry = new Button { text = "Retry loading" };
            retry.style.display = DisplayStyle.None;
            root.Add(retry);
            Action load = () => {
                retry.style.display = DisplayStyle.None;
                WorldScenes.Open(address, error => {
                    status.text = error;
                    retry.style.display = DisplayStyle.Flex;
                });
            };
            retry.clicked += load;
            load();
        }
    }
}
