using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Configuration;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class GameplayPresetsOverlay : CompositeDrawable, ISettingsTransientUi
{
    private readonly GameplayPresetStore store;
    private readonly Clipboard clipboard;
    private readonly Action close;
    private readonly BasicTextBox name;
    private readonly SpriteText status;
    private readonly SpriteText summary;
    private readonly GameplayCompactButton[] slots = new GameplayCompactButton[6];
    private readonly GameplayCompactButton saveButton, deleteButton, importButton, loadButton, exportButton, undoButton, closeButton;
    private int selected;
    private string pending;

    internal GameplayPresetsOverlay(GameplayPresetStore store, Clipboard clipboard, Action close)
    {
        this.store = store;
        this.clipboard = clipboard;
        this.close = close;
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
        var panel = new Container { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(940, 620), Masking = true, CornerRadius = 14 };
        InternalChildren = new Drawable[] { new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, 0.65f) }, panel };
        panel.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = HomeControlColours.Ivory });
        panel.Add(text("presets.title", 32, 26, 28));
        panel.Add(text("presets.scope", 32, 80, 16));
        panel.Add(text("presets.hardware", 32, 112, 15));
        for (int i = 0; i < 6; i++)
        {
            int slot = i;
            panel.Add(slots[i] = new GameplayCompactButton("", () => select(slot), 270)
            { Position = new Vector2(32 + i % 3 * 300, 166 + i / 3 * 60) });
        }
        panel.Add(name = new PresetNameTextBox
        {
            Position = new Vector2(32, 296), Size = new Vector2(870, 44),
            PlaceholderText = YokkoStrings.Get("presets.name"), LengthLimit = 32,
            FontSize = 20,
            Masking = true, CornerRadius = 7, BorderThickness = 1.5f, BorderColour = HomeControlColours.Navy,
        });
        panel.Add(summary = text("presets.empty", 32, 366, 16));
        panel.Add(status = text("presets.hint", 32, 398, 15));
        panel.Add(saveButton = button("presets.save", () => confirm("save", () => store.Save(selected, name.Text)), 32, 452));
        panel.Add(loadButton = button("presets.load", () => perform(() => store.Load(selected), "presets.loaded"), 252, 452));
        panel.Add(exportButton = button("presets.export", () => perform(() => clipboard.SetText(store.Export(selected)), "presets.copied"), 472, 452));
        panel.Add(importButton = button("presets.import", () => confirm("import", () => store.Import(selected, clipboard.GetText())), 692, 452));
        panel.Add(deleteButton = button("presets.delete", () => confirm("delete", () => store.Delete(selected)), 32, 526));
        panel.Add(undoButton = button("presets.undo", () => perform(store.Undo, "presets.undone"), 252, 526));
        panel.Add(closeButton = button("presets.close", close, 692, 526));
        select(0);
    }
    private static SpriteText text(string key, float x, float y, float size) => new SettingsReadableText
    { Position = new Vector2(x, y), Text = YokkoStrings.Get(key), Font = HomeTypography.Body(size), Colour = HomeControlColours.Navy };
    private static GameplayCompactButton button(string key, Action action, float x, float y) =>
        new(YokkoStrings.Get(key), action, 210) { Position = new Vector2(x, y) };
    protected override void LoadComplete()
    {
        base.LoadComplete();
        GetContainingFocusManager()?.ChangeFocus(name);
    }

    private void select(int slot)
    {
        selected = slot;
        pending = null;
        name.Text = store.Presets.TryGetValue(slot, out var preset) ? preset.Name : YokkoStrings.Get("presets.default_name", slot + 1).ToString();
        refresh();
    }
    private void refresh()
    {
        for (int i = 0; i < 6; i++)
        {
            slots[i].SetText(store.Presets.TryGetValue(i, out var preset) ? $"{i + 1}. {(preset.Name.Length > 12 ? preset.Name[..12] + "…" : preset.Name)}" : YokkoStrings.Get("presets.slot", i + 1));
            slots[i].IsSelected = i == selected;
        }
        bool exists = store.Presets.TryGetValue(selected, out var current);
        summary.Text = exists ? YokkoStrings.Get("presets.summary", current.Name,
            current.Numbers["gameplay.ScrollSpeed"], current.Judgement) : YokkoStrings.Get("presets.empty");
        loadButton.IsEnabled = exportButton.IsEnabled = deleteButton.IsEnabled = exists;
        undoButton.IsEnabled = store.CanUndo;
        saveButton.SetText(YokkoStrings.Get(pending == "save" ? "presets.confirm" : "presets.save"));
        importButton.SetText(YokkoStrings.Get(pending == "import" ? "presets.confirm" : "presets.import"));
        deleteButton.SetText(YokkoStrings.Get(pending == "delete" ? "presets.confirm" : "presets.delete"));
    }
    private void confirm(string operation, Action action)
    {
        if (store.Presets.ContainsKey(selected) && pending != operation)
        {
            pending = operation;
            status.Text = YokkoStrings.Get("presets.confirm_hint");
            refresh();
            return;
        }
        perform(action, operation switch
        {
            "save" => "presets.saved", "import" => "presets.pasted", "delete" => "presets.deleted", _ => "presets.done",
        });
    }
    private void perform(Action action, string successKey)
    {
        try
        {
            action();
            status.Text = YokkoStrings.Get(successKey);
            if (store.Presets.TryGetValue(selected, out var preset)) name.Text = preset.Name;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or ArgumentException)
        {
            status.Text = YokkoStrings.Get("presets.failed");
        }
        pending = null;
        refresh();
    }
    public bool DismissTransientUi()
    {
        if (pending == null) return false;
        pending = null;
        refresh();
        status.Text = YokkoStrings.Get("presets.hint");
        return true;
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Escape && !DismissTransientUi()) close();
        if (e.Key == Key.Tab)
        {
            Drawable[] controls = slots.Cast<Drawable>().Append(name)
                .Concat(new Drawable[] { saveButton, loadButton, exportButton, importButton, deleteButton, undoButton, closeButton })
                .Where(control => control.AcceptsFocus).ToArray();
            int index = Array.FindIndex(controls, control => control.HasFocus);
            int next = (index + (e.ShiftPressed ? -1 : 1) + controls.Length) % controls.Length;
            GetContainingFocusManager()?.ChangeFocus(controls[next]);
        }
        return true;
    }
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnScroll(ScrollEvent e) => true;

    private partial class PresetNameTextBox : BasicTextBox
    {
        public PresetNameTextBox()
        {
            BackgroundFocused = SettingsTheme.PaleCyan;
            BackgroundUnfocused = Color4.White;
        }
        protected override Drawable GetDrawableCharacter(char c) => new SettingsReadableText
        {
            Text = c.ToString(), Font = HomeTypography.SearchInput(20), Colour = HomeControlColours.Navy,
        };
        protected override SpriteText CreatePlaceholder() => new SettingsReadableText
        {
            Font = HomeTypography.SearchInput(20), Colour = SettingsTheme.MutedNavy,
        };
    }
}
