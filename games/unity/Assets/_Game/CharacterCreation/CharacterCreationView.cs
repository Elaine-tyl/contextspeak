using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe
{
    /// Native editor/desktop presentation; WebGL uses semantic HTML over the same 3D preview.
    public sealed class CharacterCreationView : MonoBehaviour
    {
        public event Action<string, string> Command;
        private TextField nameInput;
        private DropdownField bandInput;
        private Slider heightInput, buildInput, rotationInput;
        private Button girl, boy, confirm, cancel;
        private Label statusLabel;
        private VisualElement controls;

        private void Awake()
        {
            var root = CafeInterface.CreateRoot(gameObject);
            root.AddToClassList("character-creator");
            root.styleSheets.Add(Resources.Load<StyleSheet>("CharacterCreation"));
            controls = new ScrollView();
            controls.AddToClassList("creation-controls");
            root.Add(controls);
            Text("ContextSpeak Life", "creation-brand");
            Text("Create your character", "creation-title");
            Text("Choose your look and a starting practice goal. You can change these later at your wardrobe.", "creation-copy");
            var row = new VisualElement();
            row.AddToClassList("creation-row");
            controls.Add(row);
            girl = Button(row, "Female", "look", "Girl");
            boy = Button(row, "Male", "look", "Boy");
            nameInput = new TextField("Character name") { maxLength = 48 };
            nameInput.RegisterValueChangedCallback(change => Command?.Invoke("name", change.newValue));
            controls.Add(nameInput);
            bandInput = new DropdownField("Starting practice band", new List<string> { "3", "4", "5", "6", "7", "8", "9" }, 2);
            bandInput.RegisterValueChangedCallback(change => Command?.Invoke("band", change.newValue));
            controls.Add(bandInput);
            heightInput = Slider("Height", "height", 0.95f, 1.05f);
            buildInput = Slider("Build", "build", 0.95f, 1.05f);
            Text("Face, hair, uniform, shoes and backpack use the supplied model. Separate styles need compatible model parts. Starting band applies to new worlds; existing progress is kept.", "creation-copy");
            rotationInput = Slider("Rotate preview · 360°", "rotate", 0, 360);
            var rotate = new VisualElement();
            rotate.AddToClassList("creation-row");
            controls.Add(rotate);
            Button(rotate, "↶ Turn", "turn-left");
            Button(rotate, "Turn ↷", "turn-right");
            Button(rotate, "Reset view", "reset-preview");
            confirm = Button(controls, "Save & enter world", "confirm");
            confirm.AddToClassList("creation-primary");
            cancel = Button(controls, "Return without changes", "cancel");
            statusLabel = Text("", "creation-status");
        }

        private Label Text(string value, string style)
        {
            var label = new Label(value) { enableRichText = false };
            label.AddToClassList(style);
            controls.Add(label);
            return label;
        }

        private Button Button(VisualElement parent, string label, string action, string text = "")
        {
            var button = new Button(() => Command?.Invoke(action, text)) { text = label };
            parent.Add(button);
            return button;
        }

        private Slider Slider(string label, string action, float low, float high)
        {
            var slider = new Slider(label, low, high) { showInputField = true };
            slider.RegisterValueChangedCallback(change => Command?.Invoke(action, change.newValue.ToString(CultureInfo.InvariantCulture)));
            controls.Add(slider);
            return slider;
        }

        public void Render(CharacterProfile profile, float rotation, bool canCancel, bool busy, string status)
        {
            nameInput.SetValueWithoutNotify(profile.Name);
            bandInput.SetValueWithoutNotify(profile.Band.ToString());
            heightInput.SetValueWithoutNotify(profile.Height);
            buildInput.SetValueWithoutNotify(profile.Build);
            rotationInput.SetValueWithoutNotify(rotation);
            girl.EnableInClassList("selected", profile.Look == PlayerLook.Girl);
            boy.EnableInClassList("selected", profile.Look == PlayerLook.Boy);
            confirm.SetEnabled(!busy);
            cancel.SetEnabled(!busy);
            cancel.style.display = canCancel ? DisplayStyle.Flex : DisplayStyle.None;
            statusLabel.text = status;
        }
    }
}
