using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Bill / checkout panel: shows the lines and total, pay by card or cash from the fictional wallet.
    public sealed class PaymentController : MonoBehaviour
    {
        public static PaymentController Instance { get; private set; }
        public bool Open { get; private set; }
        private string title = "", details = "", message = "";
        private int amount;
        private bool split, tip;
        private Action paid, cancelled;
        public bool OfferTip = true;

        private void Awake() { Instance = this; }
        private void OnDestroy() { LifeUI.SetPanelOpen(this, false); if (Instance == this) Instance = null; }

        public void Show(string heading, string lines, int cents, Action onPaid, Action onCancel = null, bool offerTip = false)
        {
            title = heading;
            details = lines;
            amount = cents;
            paid = onPaid;
            cancelled = onCancel;
            OfferTip = offerTip;
            tip = false;
            message = "";
            split = false;
            Open = true;
            LifeUI.SetPanelOpen(this, true, Cancel);
        }

        public int Due
        {
            get
            {
                var total = amount + (tip ? Mathf.RoundToInt(amount * 0.1f) : 0);
                return split ? Mathf.CeilToInt(total / 2f) : total;
            }
        }

        private void Pay(string method)
        {
            var due = Due;
            if (!CurrencyManager.TrySpend(due, out message)) return;
            Open = false;
            LifeUI.SetPanelOpen(this, false);
            LifeUI.Toast(message + " (" + method + ")");
            paid?.Invoke();
        }

        public bool Cancel()
        {
            if (!Open || cancelled == null) return false;
            Open = false;
            LifeUI.SetPanelOpen(this, false);
            cancelled.Invoke();
            return true;
        }

        private void Update()
        {
            if (Open && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) LifeUI.RequestBack();
        }

        private void OnDisable() { LifeUI.SetPanelOpen(this, false); }
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); if (Open) LifeUI.SetPanelOpen(this, true, Cancel); }

        private void DrawToolkit()
        {
            if (!Open) return;
            LifeUI.Begin(out var width, out var height);
            var box = new Rect(width / 2 - 260, height / 2 - 220, 520, 440);
            GUI.Box(box, GUIContent.none, LifeUI.Panel);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 18, box.width - 48, box.height - 36));
            GUILayout.Label(title, LifeUI.Title);
            GUILayout.Label(details, LifeUI.Body);
            GUILayout.Label("Total  " + CurrencyManager.Format(amount), LifeUI.Heading);
            if (OfferTip) tip = GUILayout.Toggle(tip, " Add a 10% tip (" + CurrencyManager.Format(Mathf.RoundToInt(amount * 0.1f)) + ")", LifeUI.Body);
            split = GUILayout.Toggle(split, " Split the bill with a friend (you pay half)", LifeUI.Body);
            GUILayout.Label("You pay  " + CurrencyManager.Format(Due), LifeUI.Heading);
            GUILayout.Label("Wallet  " + CurrencyManager.Balance, LifeUI.Body);
            if (message != "") GUILayout.Label(message, LifeUI.Warning);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pay by card", LifeUI.Button)) Pay("card");
            if (GUILayout.Button("Pay in cash", LifeUI.Button)) Pay("cash");
            if (cancelled != null && GUILayout.Button("Not yet", LifeUI.Button))
            {
                Cancel();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Fictional money for language practice.", LifeUI.Small);
            GUILayout.EndArea();
        }
    }
}
