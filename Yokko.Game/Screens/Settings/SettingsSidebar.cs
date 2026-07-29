using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

/// <summary>
/// Self-contained navigation rail. Search and category visibility remain local
/// so adding settings sections only requires adding another group here.
/// </summary>
internal partial class SettingsSidebar : CompositeDrawable
{
    private readonly List<(SettingsNavHeader Header, SettingsNavItem[] Items)> navigationGroups = new();
    private readonly List<SettingsNavItem> orderedNavigationItems = new();
    private readonly Dictionary<SettingsPageKind, SettingsNavItem> navigationItems = new();
    private readonly Action<SettingsPageKind> onPageSelected;
    private readonly SettingsSearchTextBox searchBox;
    private readonly SpriteText noResults;
    private readonly Box divider;

    internal string SearchQuery => searchBox.Current.Value;
    internal bool SearchHasFocus => searchBox.HasFocus;
    internal int VisiblePageCount =>
        orderedNavigationItems.Count(item => item.IsFilteredVisible);
    internal SettingsPageKind? FocusedPage =>
        orderedNavigationItems.FirstOrDefault(item => item.HasFocus)?.Page;

    public SettingsSidebar(
        Texture logoTexture,
        Action onBack,
        SettingsPageKind selectedPage,
        Action<SettingsPageKind> onPageSelected)
    {
        this.onPageSelected = onPageSelected;
        RelativeSizeAxes = Axes.Y;
        Width = 320;

        Drawable[] navigation = createNavigation();

        InternalChildren = new Drawable[]
        {
            new Sprite
            {
                Position = new Vector2(38, 26),
                Size = new Vector2(244, 83),
                Texture = logoTexture,
            },
            new SpriteText
            {
                Position = new Vector2(38, 126),
                Text = YokkoStrings.Get("settings.title"),
                Font = HomeTypography.Display(43),
                Spacing = new Vector2(0.5f, 0),
                Colour = HomeControlColours.Navy,
            },
            new SettingsOutlineButton(YokkoStrings.Get("settings.back"), FontAwesome.Solid.ArrowLeft, onBack)
            {
                Position = new Vector2(38, 182),
            },
            searchBox = new SettingsSearchTextBox(
                SubmitSearch,
                offset => FocusAdjacentPage(null, offset))
            {
                Position = new Vector2(38, 234),
            },
            new FillFlowContainer
            {
                Position = new Vector2(30, 288),
                Width = 252,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = Vector2.Zero,
                Children = navigation,
            },
            noResults = new SpriteText
            {
                Position = new Vector2(38, 310),
                Text = YokkoStrings.Get("settings.no_matches"),
                Font = HomeTypography.Body(17),
                Colour = SettingsTheme.MutedNavy,
                Alpha = 0,
            },
            divider = new Box
            {
                Position = new Vector2(319, 28),
                Width = 1,
                Height = 664,
                Colour = SettingsTheme.Divider,
            },
        };

        searchBox.Current.BindValueChanged(e => filterNavigation(e.NewValue), true);
        SetSelected(selectedPage);
    }

    protected override void Update()
    {
        base.Update();
        divider.Height = MathF.Max(DrawHeight - 56, 0);
    }

    private Drawable[] createNavigation()
    {
        var coreHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_core"));
        SettingsNavItem general = createNavItem(SettingsPageKind.General);
        SettingsNavItem display = createNavItem(SettingsPageKind.Display);
        SettingsNavItem audio = createNavItem(SettingsPageKind.Audio);

        var creationHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_creation"));
        SettingsNavItem gameplay = createNavItem(SettingsPageKind.Gameplay);
        SettingsNavItem shortcuts = createNavItem(SettingsPageKind.Shortcuts);
        SettingsNavItem skins = createNavItem(SettingsPageKind.Skins);
        SettingsNavItem editor = createNavItem(SettingsPageKind.Editor);
        SettingsNavItem import = createNavItem(SettingsPageKind.Import);

        var systemHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_system"));
        SettingsNavItem accessibility = createNavItem(SettingsPageKind.Accessibility);
        SettingsNavItem about = createNavItem(SettingsPageKind.About);

        navigationGroups.Add((coreHeader, new[] { general, display, audio }));
        navigationGroups.Add((creationHeader, new[] { gameplay, shortcuts, skins, editor, import }));
        navigationGroups.Add((systemHeader, new[] { accessibility, about }));

        return new Drawable[]
        {
            coreHeader, general, display, audio,
            creationHeader, gameplay, shortcuts, skins, editor, import,
            systemHeader, accessibility, about,
        };
    }

    public void SetSelected(SettingsPageKind page)
    {
        foreach ((SettingsPageKind kind, SettingsNavItem item) in navigationItems)
            item.SetSelected(kind == page);
    }

    internal void SetSearchQuery(string query) =>
        searchBox.Current.Value = query ?? string.Empty;

    internal bool FocusSearch()
    {
        var focusManager = GetContainingFocusManager();
        if (focusManager == null)
            return false;

        focusManager.ChangeFocus(searchBox);
        return true;
    }

    internal bool SubmitSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return false;

        SettingsNavItem result =
            orderedNavigationItems.FirstOrDefault(
                item => item.IsFilteredVisible);
        if (result == null)
            return false;

        selectPage(result.Page);
        return true;
    }

    internal bool FocusAdjacentPage(
        SettingsPageKind? currentPage,
        int offset)
    {
        SettingsNavItem[] visible =
            orderedNavigationItems
                .Where(item => item.IsFilteredVisible)
                .ToArray();
        if (visible.Length == 0 || offset == 0)
            return false;

        int currentIndex = currentPage.HasValue
            ? Array.FindIndex(
                visible,
                item => item.Page == currentPage.Value)
            : offset > 0 ? -1 : 0;
        int nextIndex =
            (currentIndex + offset % visible.Length + visible.Length)
            % visible.Length;

        GetContainingFocusManager()?.ChangeFocus(visible[nextIndex]);
        return true;
    }

    public bool DismissTransientUi()
    {
        if (string.IsNullOrEmpty(searchBox.Current.Value))
            return false;

        searchBox.Current.Value = string.Empty;
        return true;
    }

    private SettingsNavItem createNavItem(SettingsPageKind page)
    {
        SettingsPageDefinition definition = SettingsPages.Get(page);
        var item = new SettingsNavItem(
            page,
            definition.Title,
            definition.SearchTerms,
            definition.Icon,
            () => selectPage(page),
            offset => FocusAdjacentPage(page, offset));
        navigationItems.Add(page, item);
        orderedNavigationItems.Add(item);
        return item;
    }

    private void selectPage(SettingsPageKind page)
    {
        searchBox.Current.Value = string.Empty;
        onPageSelected(page);
        GetContainingFocusManager()?.ChangeFocus(navigationItems[page]);
    }

    private void filterNavigation(string query)
    {
        string normalized = query?.Trim() ?? string.Empty;
        bool anyResults = false;

        foreach ((SettingsNavHeader header, SettingsNavItem[] items) in navigationGroups)
        {
            bool anyVisible = false;

            foreach (SettingsNavItem item in items)
            {
                bool visible = normalized.Length == 0 ||
                               item.SearchTerms.Contains(normalized, StringComparison.OrdinalIgnoreCase);
                item.SetFiltered(visible);
                anyVisible |= visible;
            }

            header.SetFiltered(anyVisible);
            anyResults |= anyVisible;
        }

        noResults.FadeTo(normalized.Length > 0 && !anyResults ? 1 : 0, 120, Easing.OutQuint);
    }
}

internal partial class SettingsSearchTextBox : BasicTextBox
{
    private readonly Func<bool> submit;
    private readonly Func<int, bool> focusResult;

    protected override float LeftRightPadding => 42;

    public SettingsSearchTextBox(
        Func<bool> submit,
        Func<int, bool> focusResult)
    {
        this.submit = submit;
        this.focusResult = focusResult;
        Size = new Vector2(244, 44);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.MutedNavy;
        BackgroundUnfocused = Color4.White;
        BackgroundFocused = SettingsTheme.PaleCyan;
        FontSize = 18;
        PlaceholderText = YokkoStrings.Get("settings.search");

        AddInternal(new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 15,
            Size = new Vector2(17),
            Icon = FontAwesome.Solid.Search,
            Colour = SettingsTheme.MutedNavy,
            Depth = -2,
        });
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.KeypadEnter)
            return submit() || base.OnKeyDown(e);

        if (e.Key == Key.Down)
            return focusResult(1) || base.OnKeyDown(e);

        if (e.Key == Key.Up)
            return focusResult(-1) || base.OnKeyDown(e);

        return base.OnKeyDown(e);
    }

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Body(18),
        Colour = HomeControlColours.Navy,
    };

    protected override SpriteText CreatePlaceholder() => new SpriteText
    {
        Font = HomeTypography.Body(18),
        Colour = SettingsTheme.MutedNavy,
    };

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Cyan;
        BorderThickness = 2;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.MutedNavy;
        BorderThickness = 1.2f;
    }
}

internal partial class SettingsOutlineButton : ClickableContainer
{
    private readonly Box background;
    private readonly float restingX;

    public override bool AcceptsFocus => true;

    public SettingsOutlineButton(LocalisableString label, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(244, 44);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.MutedNavy;
        restingX = 38;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 16,
                Size = new Vector2(17),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 51,
                Text = label,
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        this.MoveToX(restingX + 2, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        this.MoveToX(restingX, 140, Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.4f;
        background.FadeColour(SettingsTheme.PaleCyan, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.MutedNavy;
        BorderThickness = 1.2f;
        background.FadeColour(Color4.White, 100);
    }
}

internal partial class SettingsNavHeader : CompositeDrawable
{
    public SettingsNavHeader(LocalisableString label)
    {
        Size = new Vector2(252, 18);
        InternalChild = new SpriteText
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            X = 8,
            Text = label,
            Font = HomeTypography.Display(14),
            Spacing = new Vector2(1.3f, 0),
            Colour = new Color4(SettingsTheme.MutedNavy.R, SettingsTheme.MutedNavy.G, SettingsTheme.MutedNavy.B, 0.75f),
        };
    }

    public void SetFiltered(bool visible)
    {
        if (visible)
            Show();
        else
            Hide();
    }
}

internal partial class SettingsNavItem : ClickableContainer
{
    private readonly Func<int, bool> navigate;
    private readonly Box background;
    private readonly Box selectionBar;
    private readonly Box selectionCorner;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly SpriteIcon plus;
    private bool selected;

    public SettingsPageKind Page { get; }
    public string SearchTerms { get; }
    public bool IsFilteredVisible { get; private set; } = true;
    public override bool AcceptsFocus => true;

    public SettingsNavItem(
        SettingsPageKind page,
        LocalisableString label,
        string searchTerms,
        IconUsage itemIcon,
        Action action,
        Func<int, bool> navigate)
    {
        Page = page;
        SearchTerms = searchTerms;
        Action = action;
        this.navigate = navigate;
        Size = new Vector2(252, 35);
        Masking = true;
        CornerRadius = 7;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            selectionBar = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 22,
                Size = new Vector2(17),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 57,
                Text = label,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            plus = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -17,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            selectionCorner = new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
                Alpha = 0,
            },
        };
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.Transparent, 120, Easing.OutQuint);
        selectionBar.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectionCorner.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        text.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        plus.FadeColour(selected ? HomeControlColours.Yellow : HomeControlColours.Pink, 120, Easing.OutQuint);
    }

    public void SetFiltered(bool visible)
    {
        IsFilteredVisible = visible;

        if (visible)
            Show();
        else
            Hide();
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
        {
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
            icon.FadeColour(HomeControlColours.Cyan, 120, Easing.OutQuint);
            plus.RotateTo(90, 120, Easing.OutQuint);
        }
        else
        {
            background.FadeColour(SettingsTheme.HoverNavy, 120, Easing.OutQuint);
        }

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.Transparent, 140, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 140, Easing.OutQuint);
        plus.RotateTo(0, 140, Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Down)
            return navigate(1) || base.OnKeyDown(e);

        if (e.Key == Key.Up)
            return navigate(-1) || base.OnKeyDown(e);

        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.2f;
        if (!selected)
            background.FadeColour(SettingsTheme.PaleCyan, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderThickness = 0;
        background.FadeColour(
            selected ? HomeControlColours.Navy : Color4.Transparent,
            100);
    }
}
