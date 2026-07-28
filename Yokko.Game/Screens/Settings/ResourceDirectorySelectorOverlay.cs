using System;
using System.IO;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class ResourceDirectorySelectorOverlay : CompositeDrawable
{
    private readonly BasicDirectorySelector selector;
    private readonly Action<string> onSelected;
    private readonly Action onUseDefault;

    public ResourceDirectorySelectorOverlay(
        Action<string> onSelected,
        Action onUseDefault)
    {
        this.onSelected = onSelected;
        this.onUseDefault = onUseDefault;
        RelativeSizeAxes = Axes.Both;
        Depth = -100;

        InternalChildren = new Drawable[]
        {
            new ClickableContainer
            {
                RelativeSizeAxes = Axes.Both,
                Action = () => Dismiss(),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.01f, 0.02f, 0.08f, 0.72f),
                },
            },
            new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(900, 580),
                Masking = true,
                CornerRadius = 12,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Cyan,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(28, 22),
                        Text = YokkoStrings.Get(
                            "settings.import.resource_selector_title"),
                        Font = HomeTypography.Display(25),
                        Colour = HomeControlColours.Navy,
                    },
                    selector = new BasicDirectorySelector
                    {
                        Position = new Vector2(28, 64),
                        Size = new Vector2(844, 438),
                    },
                    new SettingsSkinActionButton(
                        YokkoStrings.Get("settings.import.resource_default"),
                        FontAwesome.Solid.Undo,
                        useDefault,
                        false)
                    {
                        Position = new Vector2(28, 522),
                        Width = 150,
                    },
                    new SettingsSkinActionButton(
                        YokkoStrings.Get("settings.import.resource_cancel"),
                        FontAwesome.Solid.Times,
                        () => Dismiss(),
                        false)
                    {
                        Position = new Vector2(634, 522),
                    },
                    new SettingsSkinActionButton(
                        YokkoStrings.Get("settings.import.resource_select"),
                        FontAwesome.Solid.Check,
                        selectCurrent,
                        true)
                    {
                        Position = new Vector2(768, 522),
                    },
                },
            },
        };

        Hide();
    }

    public void Open(string initialPath)
    {
        if (!string.IsNullOrWhiteSpace(initialPath)
            && Directory.Exists(initialPath))
        {
            selector.CurrentPath.Value = new DirectoryInfo(initialPath);
        }

        Show();
        this.FadeIn(150, Easing.OutQuint);
    }

    public bool Dismiss()
    {
        if (!IsPresent)
            return false;

        this.FadeOut(100).OnComplete(_ => Hide());
        return true;
    }

    private void selectCurrent()
    {
        string path = selector.CurrentPath.Value?.FullName;

        if (string.IsNullOrWhiteSpace(path))
            return;

        Dismiss();
        onSelected(path);
    }

    private void useDefault()
    {
        Dismiss();
        onUseDefault();
    }
}
