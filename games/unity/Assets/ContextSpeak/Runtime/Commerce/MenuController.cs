using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// The printed menu as an interactive panel: category tabs, dish cards with price, description
    /// and allergens, quantity controls, customisations, a cart and order confirmation.
    public sealed class MenuController : MonoBehaviour
    {
        public static MenuController Instance { get; private set; }
        public bool Open { get; private set; }
        public Cart Cart { get; private set; } = new Cart();
        private MenuCatalog catalog;
        private string category = "";
        private Action<Cart> confirmed;
        private Action cancelled;
        private readonly Dictionary<string, int[]> selections = new Dictionary<string, int[]>();
        private Vector2 scroll;

        private void Awake() { Instance = this; }
        private void OnDestroy() { LifeUI.SetPanelOpen(this, false); if (Instance == this) Instance = null; }

        public void Show(MenuCatalog menu, Action<Cart> onConfirm, Action onCancel = null)
        {
            catalog = menu;
            Cart = new Cart();
            category = menu.Categories.FirstOrDefault(name => menu.Items.Any(item => item.category == name)) ?? "";
            confirmed = onConfirm;
            cancelled = onCancel;
            selections.Clear();
            Open = true;
            LifeUI.SetPanelOpen(this, true, Back);
        }

        public void Close(bool confirm)
        {
            if (!Open) return;
            Open = false;
            LifeUI.SetPanelOpen(this, false);
            if (confirm) confirmed?.Invoke(Cart); else cancelled?.Invoke();
        }

        private void Update()
        {
            if (Open && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) LifeUI.RequestBack();
        }

        private bool Back() { Close(false); return true; }
        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (Open) LifeUI.SetPanelOpen(this, true, Back); }

        private void DrawToolkit()
        {
            if (!Open || catalog == null) return;
            LifeUI.Begin(out var width, out var height);
            var box = new Rect(width / 2 - 520, 50, 1040, height - 100);
            GUI.Box(box, GUIContent.none, LifeUI.Panel);
            GUILayout.BeginArea(new Rect(box.x + 20, box.y + 16, box.width - 40, box.height - 32));
            GUILayout.Label(catalog.Title, LifeUI.Title);
            GUILayout.BeginHorizontal();
            foreach (var name in catalog.Categories.Where(name => catalog.Items.Any(item => item.category == name)))
                if (GUILayout.Toggle(category == name, name, LifeUI.Tab)) category = name;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(650));
            foreach (var item in catalog.Items.Where(item => item.category == category)) Card(item);
            GUILayout.EndScrollView();
            GUILayout.BeginVertical(LifeUI.Paper, GUILayout.Width(330));
            GUILayout.Label("Your order", LifeUI.Heading);
            foreach (var line in Cart.Lines.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(line.name + (line.choices != "" ? "\n<size=13>" + line.choices + "</size>" : ""), LifeUI.Dark, GUILayout.Width(170));
                if (GUILayout.Button("−", LifeUI.Small, GUILayout.Width(28))) Cart.Change(line, -1);
                GUILayout.Label(line.quantity.ToString(), LifeUI.Dark, GUILayout.Width(20));
                if (GUILayout.Button("+", LifeUI.Small, GUILayout.Width(28))) Cart.Change(line, 1);
                GUILayout.Label(CurrencyManager.Format(line.Total), LifeUI.Dark);
                GUILayout.EndHorizontal();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("Total  " + CurrencyManager.Format(Cart.Total) + "      Wallet  " + CurrencyManager.Balance, LifeUI.Heading);
            GUI.enabled = Cart.Count > 0;
            if (GUILayout.Button("Confirm order", LifeUI.Button)) Close(true);
            GUI.enabled = true;
            if (GUILayout.Button("Close menu", LifeUI.Button)) Close(false);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Card(MenuEntry item)
        {
            GUILayout.BeginHorizontal(LifeUI.Paper);
            // The "photo": a colour plate with the dish's short label (swap in a texture when you have photos).
            var plate = GUILayoutUtility.GetRect(96, 96, GUILayout.Width(96), GUILayout.Height(96));
            var previous = GUI.color;
            GUI.color = item.colour;
            GUI.DrawTexture(plate, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(plate, item.icon != "" ? item.icon : item.name.Substring(0, 1), LifeUI.Plate);
            GUILayout.BeginVertical();
            GUILayout.Label("<b>" + item.name + "</b>   " + CurrencyManager.Format(item.cents), LifeUI.Dark);
            GUILayout.Label(item.description, LifeUI.DarkSmall);
            if (item.allergens != "") GUILayout.Label("Allergens: " + item.allergens, LifeUI.Warning);
            var groups = item.OptionGroups.ToArray();
            if (!selections.TryGetValue(item.id, out var chosen) || chosen.Length != groups.Length) selections[item.id] = chosen = new int[groups.Length];
            for (var index = 0; index < groups.Length; index++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(groups[index].group, LifeUI.DarkSmall, GUILayout.Width(70));
                var labels = groups[index].choices.Select(choice => { var (label, cents) = MenuEntry.Choice(choice); return cents > 0 ? label + " +" + CurrencyManager.Format(cents) : label; }).ToArray();
                chosen[index] = GUILayout.Toolbar(chosen[index], labels, LifeUI.Small);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Add to order", LifeUI.Small, GUILayout.Width(140)))
                Cart.Add(item, groups.Select((group, index) => group.choices[chosen[index]]).ToArray());
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }
    }
}
