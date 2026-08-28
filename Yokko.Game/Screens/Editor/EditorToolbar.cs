using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osuTK;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The ivory tool row directly below the header. It owns the chart lifecycle
/// actions (new 4K/7K, import, export, playtest) as shared Yokko buttons.
/// </summary>
public partial class EditorToolbar : CompositeDrawable
{
    public EditorToolbar(
        Action newFourKey,
        Action newSevenKey,
        Action importChart,
        Action exportChart,
        Action playtest)
    {
        Size = new Vector2(EditorScreen.CanvasWidth, EditorScreen.ToolbarHeight);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorTheme.Ivory,
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 24,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    new EditorToolButton(YokkoStrings.Get("editor.new_4k"), newFourKey),
                    new EditorToolButton(YokkoStrings.Get("editor.new_7k"), newSevenKey),
                    new EditorToolButton(YokkoStrings.Get("editor.import"), importChart),
                    new EditorToolButton(YokkoStrings.Get("editor.export"), exportChart),
                    new EditorToolButton(
                        YokkoStrings.Get("editor.playtest"),
                        playtest,
                        YokkoAccentRole.Positive),
                },
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = EditorTheme.Border(0.14f),
            },
        };
    }
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
            112,
            40,
            YokkoButtonStyle.Accent,
            null,
            13,
            null,
            accentRole)
    {
    }
}
