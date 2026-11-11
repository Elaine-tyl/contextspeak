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
            var life = Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var url)
                && url.Query.TrimStart('?').Split('&').Contains("world=life");
            var root = CafeInterface.CreateRoot(gameObject);
            root.style.display = DisplayStyle.Flex;
            var status = new Label("ContextSpeak · Loading your neighborhood…");
            root.Add(status);
            var retry = new Button { text = "Retry loading" };
            retry.style.display = DisplayStyle.None;
            root.Add(retry);
            Action load = () => {
                retry.style.display = DisplayStyle.None;
                WorldScenes.Open(life ? "LifeTown" : "ForestCafe", error => {
                    status.text = error;
                    retry.style.display = DisplayStyle.Flex;
                });
            };
            retry.clicked += load;
            load();
        }
    }
}
