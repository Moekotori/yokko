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
    private readonly Action<SettingsPageKind, string> onPageSelected;
    private readonly SettingsSearchTextBox searchBox;
    private readonly SpriteText noResults;
    private readonly Box divider;
    private readonly Container selectionGlider;
    private readonly SettingsContentScrollContainer navigationScroll;
    private readonly FillFlowContainer navigationFlow;
    private float gliderTargetY = -1;

    internal const float NavigationTop = 288;
    internal const float NavigationHeight = 364;

    internal float NavigationScrollPosition => (float)navigationScroll.Current;
    internal float NavigationScrollableExtent => (float)navigationScroll.ScrollableExtent;

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
        Action<SettingsPageKind, string> onPageSelected)
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
            new HomeRing(20, 2.5f, HomeControlColours.Cyan)
            {
                Position = new Vector2(272, 22),
            },
            new HomeTwinkle(11, 2300)
            {
                Position = new Vector2(296, 92),
                Colour = HomeControlColours.Pink,
            },
            new HomeDotField
            {
                Position = new Vector2(198, 128),
                Size = new Vector2(84, 40),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.1f),
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
            navigationScroll = new SettingsContentScrollContainer
            {
                Position = new Vector2(30, NavigationTop),
                Size = new Vector2(252, NavigationHeight),
                Child = navigationFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = Vector2.Zero,
                    Children = navigation,
                },
            },
            noResults = new SpriteText
            {
                Position = new Vector2(38, 310),
                Text = YokkoStrings.Get("settings.no_matches"),
                Font = HomeTypography.Body(17),
                Colour = SettingsTheme.MutedNavy,
                Alpha = 0,
            },
            // 沿导航左缘滑动到当前选中项的指示条。
            selectionGlider = new Container
            {
                Position = new Vector2(22, 292),
                Size = new Vector2(5, 27),
                Masking = true,
                CornerRadius = 2.5f,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Pink,
                },
            },
            divider = new Box
            {
                Position = new Vector2(319, 28),
                Width = 1,
                Height = 664,
                Colour = SettingsTheme.Divider,
            },
            new HomeBeatPips(
                new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.32f),
                HomeControlColours.Pink)
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(38, -54),
            },
            new HomePulseBeacon(18, HomeControlColours.Cyan, HomeControlColours.Pink)
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(180, -30),
            },
        };

        searchBox.Current.BindValueChanged(e => filterNavigation(e.NewValue), true);
        SetSelected(selectedPage);
    }

    protected override void Update()
    {
        base.Update();
        divider.Height = MathF.Max(DrawHeight - 56, 0);
        updateSelectionGlider();
    }

    private void updateSelectionGlider()
    {
        var selectedItem = orderedNavigationItems.FirstOrDefault(item => item.IsSelected);

        if (selectedItem?.IsFilteredVisible != true)
        {
            gliderTargetY = -1;
            selectionGlider.Hide();
            return;
        }

        float target = ToLocalSpace(
            selectedItem.ScreenSpaceDrawQuad.TopLeft).Y + 4;
        selectionGlider.Show();

        if (MathF.Abs(target - gliderTargetY) > 0.5f)
        {
            gliderTargetY = target;
            selectionGlider.MoveToY(target, 260, Easing.OutQuint);
        }
    }

    private Drawable[] createNavigation()
    {
        var coreHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_core"));
        SettingsNavItem general = createNavItem(SettingsPageKind.General);
        SettingsNavItem display = createNavItem(SettingsPageKind.Display);
        SettingsNavItem audio = createNavItem(SettingsPageKind.Audio);
        SettingsNavItem accessibility = createNavItem(SettingsPageKind.Accessibility);

        var creationHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_creation"));
        SettingsNavItem gameplay = createNavItem(SettingsPageKind.Gameplay);
        SettingsNavItem mods = createNavItem(SettingsPageKind.Mods);
        SettingsNavItem shortcuts = createNavItem(SettingsPageKind.Shortcuts);
        SettingsNavItem skins = createNavItem(SettingsPageKind.Skins);
        SettingsNavItem import = createNavItem(SettingsPageKind.Import);
        SettingsNavItem editor = createNavItem(SettingsPageKind.Editor);

        var systemHeader = new SettingsNavHeader(YokkoStrings.Get("settings.group_system"));
        SettingsNavItem desktop = SettingsNavigation.IsVisible(SettingsPageKind.Desktop)
            ? createNavItem(SettingsPageKind.Desktop)
            : null;
        SettingsNavItem safety = createNavItem(SettingsPageKind.Safety);
        SettingsNavItem about = createNavItem(SettingsPageKind.About);

        navigationGroups.Add((coreHeader, new[] { general, display, audio, accessibility }));
        navigationGroups.Add((creationHeader, new[] { gameplay, mods, shortcuts, skins, import, editor }));

        var systemItems = new List<SettingsNavItem> { safety, about };
        if (desktop != null)
            systemItems.Insert(0, desktop);
        navigationGroups.Add((systemHeader, systemItems.ToArray()));

        var navigation = new List<Drawable>
        {
            coreHeader, general, display, audio, accessibility,
            creationHeader, gameplay, mods, shortcuts, skins, import, editor,
            systemHeader,
        };
        if (desktop != null)
            navigation.Add(desktop);
        navigation.Add(safety);
        navigation.Add(about);
        return navigation.ToArray();
    }

    public void SetSelected(SettingsPageKind page)
    {
        foreach ((SettingsPageKind kind, SettingsNavItem item) in navigationItems)
            item.SetSelected(kind == page);

        if (navigationItems.TryGetValue(page, out SettingsNavItem selected)
            && selected.IsFilteredVisible)
        {
            scrollNavigationItemIntoView(selected);
        }
    }

    private void scrollNavigationItemIntoView(SettingsNavItem item)
    {
        if (navigationScroll.ScrollableExtent <= 0.5)
        {
            navigationScroll.ScrollToStart(false);
            return;
        }

        float itemTop = item.ToSpaceOfOtherDrawable(Vector2.Zero, navigationFlow).Y;
        float itemBottom = itemTop + item.DrawHeight;
        float visibleTop = (float)navigationScroll.Current;
        float visibleBottom = visibleTop + navigationScroll.DrawHeight;

        if (itemTop < visibleTop)
            navigationScroll.ScrollTo(itemTop, true);
        else if (itemBottom > visibleBottom)
            navigationScroll.ScrollTo(itemBottom - navigationScroll.DrawHeight, true);
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

        SettingsSearchMatch? match = SettingsSearchCatalog.FindBest(SearchQuery);
        if (match == null)
            return false;

        searchBox.Current.Value = string.Empty;
        onPageSelected(match.Value.Page, match.Value.ItemId);
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
        if (!SettingsNavigation.IsVisible(page))
            throw new InvalidOperationException(
                $"Settings page {page} is not visible on this platform.");

        SettingsPageDefinition definition = SettingsPages.Get(page);
        var item = new SettingsNavItem(
            page,
            definition.Title,
            definition.TitleSearchTerms,
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
        onPageSelected(page, null);
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
                int score = SettingsSearchMatcher.Score(
                    normalized,
                    item.TitleSearchTerms,
                    item.SearchTerms);
                bool visible = score != SettingsSearchMatcher.NoMatch;
                item.SearchScore = score;
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
        BorderThickness = 1.5f;
        BorderColour = HomeControlColours.Navy;
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
        Font = HomeTypography.SearchInput(18),
        Colour = HomeControlColours.Navy,
    };

    protected override SpriteText CreatePlaceholder() => new SpriteText
    {
        Font = HomeTypography.SearchInput(18),
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
        BorderColour = HomeControlColours.Navy;
        BorderThickness = 1.5f;
    }
}

internal partial class SettingsOutlineButton : ClickableContainer
{
    private readonly Box background;
    private readonly Container cardBody;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly float restingX;
    private float pressedRestingY;
    private bool isPressed;

    public override bool AcceptsFocus => true;

    public SettingsOutlineButton(LocalisableString label, IconUsage buttonIcon, Action action)
    {
        Action = action;
        Size = new Vector2(244, 44);
        restingX = 38;

        InternalChildren = new Drawable[]
        {
            // 主页贴纸卡片的偏移底衬，让按钮像一枚贴纸。
            new Container
            {
                Position = new Vector2(0, 3),
                Size = new Vector2(244, 41),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.18f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(247, 47),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            cardBody = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                },
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 16,
                Size = new Vector2(17),
                Icon = buttonIcon,
                Colour = HomeControlColours.Navy,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 51,
                Text = label,
                Font = HomeTypography.Control(19),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        icon.MoveToX(12, 130, Easing.OutQuint);
        this.MoveToX(restingX + 3, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        icon.MoveToX(16, 150, Easing.OutQuint);
        this.MoveToX(restingX, 140, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (!isPressed)
        {
            pressedRestingY = Y;
            isPressed = true;
        }

        this.MoveToY(CalculatePressedY(pressedRestingY), 80, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (isPressed)
        {
            this.MoveToY(pressedRestingY, 120, Easing.OutQuint);
            isPressed = false;
        }

        base.OnMouseUp(e);
    }

    internal static float CalculatePressedY(float restingY) =>
        restingY + 2;

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
        cardBody.BorderColour = HomeControlColours.Pink;
        cardBody.BorderThickness = 2.4f;
        background.FadeColour(SettingsTheme.PaleCyan, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        cardBody.BorderColour = HomeControlColours.Navy;
        cardBody.BorderThickness = 1.5f;
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
    public string TitleSearchTerms { get; }
    public string SearchTerms { get; }
    public int SearchScore { get; set; }
    public bool IsFilteredVisible { get; private set; } = true;
    internal bool IsSelected => selected;
    public override bool AcceptsFocus => true;

    public SettingsNavItem(
        SettingsPageKind page,
        LocalisableString label,
        string titleSearchTerms,
        string searchTerms,
        IconUsage itemIcon,
        Action action,
        Func<int, bool> navigate)
    {
        Page = page;
        TitleSearchTerms = titleSearchTerms;
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
        bool becameSelected = isSelected && !selected;
        selected = isSelected;
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.Transparent, 120, Easing.OutQuint);
        selectionBar.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        selectionCorner.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        text.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        plus.FadeColour(selected ? HomeControlColours.Yellow : HomeControlColours.Pink, 120, Easing.OutQuint);

        if (becameSelected)
        {
            selectionBar.ScaleTo(new Vector2(1, 0.3f))
                        .ScaleTo(Vector2.One, 240, Easing.OutBack);
            icon.MoveToX(26).MoveToX(22, 220, Easing.OutQuint);
            text.MoveToX(61).MoveToX(57, 220, Easing.OutQuint);
        }
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
            icon.MoveToX(26, 120, Easing.OutQuint)
                .RotateTo(-10, 140, Easing.OutQuint);
            text.MoveToX(61, 130, Easing.OutQuint);
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
        icon.MoveToX(22, 150, Easing.OutQuint)
            .RotateTo(0, 170, Easing.OutQuint);
        text.MoveToX(57, 150, Easing.OutQuint);
        plus.RotateTo(0, 140, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.97f, 400, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(1f, 220, Easing.OutQuint);
        base.OnMouseUp(e);
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
