using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe
{
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var query = Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var url) ? url.Query.TrimStart('?').Split('&') : new string[0];
            // Desktop builds open the Forest Café slice; the web keeps the original café unless ?world= says otherwise.
            var address = query.Contains("world=life") ? "LifeTown" : query.Contains("world=slice") ? "ForestSlice"
                : query.Contains("world=cafe") || Application.platform == RuntimePlatform.WebGLPlayer ? "ForestCafe" : "ForestSlice";
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
