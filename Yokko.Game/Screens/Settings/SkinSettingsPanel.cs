using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Settings;

internal partial class SkinSettingsPanel : CompositeDrawable
{
    private readonly OsuManiaSkinLibrary library;
    private readonly FillFlowContainer skinList;

    internal int SkinCount => skinList.Children.Count;

    public SkinSettingsPanel(OsuManiaSkinLibrary library)
    {
        this.library = library;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(378, 42),
                Text = YokkoStrings.Get("settings.skins.title"),
                Font = HomeTypography.Display(58),
                Spacing = new Vector2(0.45f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(378, 105),
                Text = YokkoStrings.Get("settings.skins.subtitle"),
                Font = HomeTypography.Body(20),
                Colour = SettingsTheme.MutedNavy,
            },
            createDropCard(),
            new SpriteText
            {
                Position = new Vector2(378, 272),
                Text = YokkoStrings.Get("settings.skins.section_library"),
                Font = HomeTypography.Display(24),
                Colour = HomeControlColours.Navy,
            },
            new BasicScrollContainer
            {
                Position = new Vector2(378, 310),
                Size = new Vector2(840, 282),
                ScrollbarVisible = false,
                Child = skinList = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                },
            },
            new SettingsPanelFooter(
                YokkoStrings.Get("settings.skins.drop_hint")),
        };

        library.LibraryChanged += onLibraryChanged;
        refresh();
    }

    internal bool SelectSkin(string id) => library.Select(id);

    internal bool DeleteSkin(string id) => library.Delete(id);

    private Drawable createDropCard() => new Container
    {
        Position = new Vector2(378, 156),
        Size = new Vector2(840, 88),
        Masking = true,
        CornerRadius = 9,
        BorderThickness = 1.2f,
        BorderColour = HomeControlColours.Cyan,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 50,
                Size = new Vector2(56),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 50,
                Size = new Vector2(25),
                Icon = FontAwesome.Solid.Download,
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 94,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.section_import"),
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.drop_hint"),
                        Font = HomeTypography.Body(17),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -28,
                Text = ".OSK  /  FOLDER",
                Font = HomeTypography.Display(15),
                Spacing = new Vector2(1),
                Colour = HomeControlColours.Pink,
            },
        },
    };

    private void onLibraryChanged() => Schedule(refresh);

    private void refresh()
    {
        skinList.Clear();
        var entries = library.GetInstalledSkins();

        if (entries.Count == 0)
        {
            skinList.Add(new EmptySkinLibraryCard());
            return;
        }

        foreach (OsuManiaSkinEntry entry in entries)
        {
            skinList.Add(new SkinLibraryRow(
                entry,
                library.IsSelected(entry.Id),
                () => library.Select(entry.Id),
                () => library.Delete(entry.Id)));
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            library.LibraryChanged -= onLibraryChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class EmptySkinLibraryCard : CompositeDrawable
{
    public EmptySkinLibraryCard()
    {
        RelativeSizeAxes = Axes.X;
        Height = 114;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(28),
                Icon = FontAwesome.Regular.Image,
                Colour = SettingsTheme.MutedNavy,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 88,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.empty"),
                        Font = HomeTypography.Display(22),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("settings.skins.empty_note"),
                        Font = HomeTypography.Body(17),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
        };
    }
}

internal partial class SkinLibraryRow : CompositeDrawable
{
    private readonly SettingsSkinActionButton deleteButton;
    private readonly Action onDelete;
    private bool awaitingDeleteConfirmation;

    public SkinLibraryRow(
        OsuManiaSkinEntry entry,
        bool selected,
        Action onSelect,
        Action onDelete)
    {
        this.onDelete = onDelete;
        RelativeSizeAxes = Axes.X;
        Height = 76;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = selected ? 2 : 1.2f;
        BorderColour = selected ? HomeControlColours.Cyan : SettingsTheme.Divider;

        string details = string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(entry.Author) ? null : entry.Author,
            entry.KeyModes.Count == 0
                ? null
                : string.Join(" / ", entry.KeyModes.Select(keys => $"{keys}K")),
        }.Where(value => value != null));

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected ? SettingsTheme.PaleCyan : Color4.White,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 7,
                Colour = selected ? HomeControlColours.Cyan : HomeControlColours.Yellow,
            },
            new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 26,
                Width = 510,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Width = 510,
                        Truncate = true,
                        Text = entry.Name,
                        Font = HomeTypography.Display(21),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Width = 510,
                        Truncate = true,
                        Text = details,
                        Font = HomeTypography.Body(15),
                        Colour = SettingsTheme.MutedNavy,
                    },
                },
            },
            new SettingsSkinActionButton(
                selected
                    ? YokkoStrings.Get("settings.skins.active")
                    : YokkoStrings.Get("settings.skins.use"),
                selected ? FontAwesome.Solid.Check : FontAwesome.Solid.Play,
                onSelect,
                selected)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -130,
            },
            deleteButton = new SettingsSkinActionButton(
                YokkoStrings.Get("settings.skins.delete"),
                FontAwesome.Solid.Trash,
                requestDelete,
                false,
                destructive: true)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -16,
            },
        };
    }

    private void requestDelete()
    {
        if (awaitingDeleteConfirmation)
        {
            onDelete();
            return;
        }

        awaitingDeleteConfirmation = true;
        deleteButton.SetLabel(YokkoStrings.Get("settings.skins.confirm_delete"));
        Scheduler.AddDelayed(() =>
        {
            awaitingDeleteConfirmation = false;
            deleteButton.SetLabel(YokkoStrings.Get("settings.skins.delete"));
        }, 2500);
    }
}

internal partial class SettingsSkinActionButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText label;
    private readonly bool selected;
    private readonly bool destructive;

    public SettingsSkinActionButton(
        LocalisableString text,
        IconUsage icon,
        Action action,
        bool selected,
        bool destructive = false)
    {
        Action = action;
        this.selected = selected;
        this.destructive = destructive;
        Size = new Vector2(104, 38);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1.2f;
        BorderColour = destructive ? HomeControlColours.Pink : HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected ? HomeControlColours.Navy : Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 18,
                Size = new Vector2(14),
                Icon = icon,
                Colour = selected
                    ? Color4.White
                    : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                X = 9,
                Text = text,
                Font = HomeTypography.Display(14),
                Colour = selected
                    ? Color4.White
                    : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            },
        };
    }

    public void SetLabel(LocalisableString text) => label.Text = text;

    protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
    {
        if (!selected)
            background.FadeColour(destructive ? HomeControlColours.Pink : SettingsTheme.PaleCyan, 100);

        if (destructive)
            label.FadeColour(Color4.White, 100);

        return true;
    }

    protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
    {
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.White, 120);
        label.FadeColour(
            selected ? Color4.White : destructive ? HomeControlColours.Pink : HomeControlColours.Navy,
            120);
    }
}
