using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using Yokko.Game.Presentation;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorHeader : CompositeDrawable
{
    private readonly EditorToolButton importButton;

    public EditorHeader(Action newFourKey, Action newSevenKey, Action importChart, Action exportOsu, Action playtest)
    {
        Width = 1122;
        Height = 70;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new YokkoText(
                        YokkoStrings.Get("editor.title"),
                        29,
                        YokkoTextStyle.Heading)
                    {
                    },
                    new YokkoText(
                        YokkoStrings.Get("editor.subtitle"),
                        12,
                        YokkoTextStyle.Body,
                        YokkoTextColourRole.Muted)
                    {
                    },
                },
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new EditorToolButton(YokkoStrings.Get("editor.new_4k"), newFourKey),
                    new EditorToolButton(YokkoStrings.Get("editor.new_7k"), newSevenKey),
                    importButton = new EditorToolButton(YokkoStrings.Get("editor.import"), importChart),
                    new EditorToolButton(YokkoStrings.Get("editor.export"), exportOsu),
                    new EditorToolButton(
                        YokkoStrings.Get("editor.playtest"),
                        playtest,
                        YokkoAccentRole.Positive),
                },
            },
        };
    }

    internal void SetImportEnabled(bool enabled) => importButton.IsEnabled = enabled;
}

public partial class EditorToolButton : YokkoButton
{
    public EditorToolButton(
        LocalisableString text,
        Action action,
        YokkoAccentRole accentRole = YokkoAccentRole.Accent)
        : base(
            text,
            action,
            104,
            42,
            YokkoButtonStyle.Accent,
            null,
            13,
            null,
            accentRole)
    {
    }
}
