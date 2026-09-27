using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>Task-oriented guidance for every category, on the existing settings stage.</summary>
internal partial class SettingsHelpOverlay : CompositeDrawable
{
    private readonly Action close;
    public override bool AcceptsFocus => true;

    internal SettingsHelpOverlay(SettingsPageKind page, Action close)
    {
        this.close = close;
        RelativeSizeAxes = Axes.Both;
        Depth = -1000;
        string prefix = $"settings.guide.{page.ToString().ToLowerInvariant()}";
        var panel = new Container
        {
            Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(900, 590),
            Masking = true, CornerRadius = 14,
        };
        InternalChildren = new Drawable[]
        {
            new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, 0.65f) }, panel,
        };
        panel.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = HomeControlColours.Ivory });
        panel.Add(new SettingsReadableText
        {
            Position = new Vector2(32, 30), Text = YokkoStrings.Get("settings.guide.heading", SettingsPages.Get(page).Title),
            Font = HomeTypography.Display(28), Colour = HomeControlColours.Navy,
        });
        for (int i = 0; i < 3; i++)
        {
            panel.Add(new SettingsReadableText
            {
                Position = new Vector2(32, 110 + i * 118), Text = YokkoStrings.Get($"{prefix}.{i}.title"),
                Font = HomeTypography.Display(21), Colour = HomeControlColours.Navy,
            });
            var description = new TextFlowContainer(sprite =>
            {
                sprite.Font = HomeTypography.Body(17);
                sprite.Colour = HomeControlColours.Navy;
            }) { Position = new Vector2(32, 145 + i * 118), Width = 836, AutoSizeAxes = Axes.Y };
            description.AddText(YokkoStrings.Get($"{prefix}.{i}.body"));
            panel.Add(description);
        }
        panel.Add(new GameplayCompactButton(YokkoStrings.Get("settings.guide.back"), close, 210)
        { Position = new Vector2(658, 516), IsSelected = true });
    }
    protected override void LoadComplete() { base.LoadComplete(); GetContainingFocusManager()?.ChangeFocus(this); }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Escape or Key.Enter or Key.KeypadEnter) close();
        return true;
    }
    protected override bool OnMouseDown(MouseDownEvent e) => true;
    protected override bool OnScroll(ScrollEvent e) => true;
}

internal enum SettingsQuickAction { None, Calibration, Presets }

internal static class SettingsQuickActions
{
    internal static SettingsQuickAction Find(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return SettingsQuickAction.None;
        string value = string.Concat(System.Linq.Enumerable.Where(query.ToLowerInvariant(), char.IsLetterOrDigit));
        return value switch
        {
            "延迟" or "调延迟" or "校准" or "延迟校准" or "打击不同步" or "声音不同步" or "不跟手"
                or "calibrate" or "calibration" or "latency" or "timingoffset" or "offset"
                or "遅延" or "タイミング調整" => SettingsQuickAction.Calibration,
            "预设" or "配置预设" or "玩法配置" or "presets" or "preset" or "プレイ設定プリセット" => SettingsQuickAction.Presets,
            _ => SettingsQuickAction.None,
        };
    }
}
