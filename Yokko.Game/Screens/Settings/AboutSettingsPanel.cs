using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Configuration;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class AboutSettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private readonly CancellationTokenSource updateCheckCancellation = new();
    private readonly Container contentRoot;
    private SpriteText updateStatusText;
    private bool checkingForUpdates;
    private bool disposed;

    public AboutSettingsPanel()
    {
        RelativeSizeAxes = Axes.Both;

        string version = Assembly.GetEntryAssembly()?
                                 .GetName().Version?.ToString()
                         ?? "development";

        InternalChild = contentRoot = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                SettingsChrome.CreateHeader(
                    YokkoStrings.Get("settings.about.title"),
                    YokkoStrings.Get("settings.about.subtitle"),
                    FontAwesome.Solid.InfoCircle,
                    (int)SettingsPageKind.About + 1),
                createInformationCard(
                    174,
                    FontAwesome.Solid.InfoCircle,
                    YokkoStrings.Get("settings.about.section_version"),
                    version,
                    SettingsTheme.StatusCyan),
                createUpdateCard(),
                createInformationCard(
                    382,
                    FontAwesome.Solid.Pen,
                    YokkoStrings.Get("settings.about.section_credits"),
                    YokkoStrings.Get("settings.about.creator"),
                    SettingsTheme.PaleCyan),
                createInformationCard(
                    486,
                    FontAwesome.Solid.Heart,
                    YokkoStrings.Get("settings.about.section_acknowledgements"),
                    YokkoStrings.Get("settings.about.acknowledgements"),
                    Color4.White),
            },
        };
    }

    internal bool IsCheckingForUpdates => checkingForUpdates;

    internal void CheckForUpdates()
    {
        if (checkingForUpdates)
            return;

        checkingForUpdates = true;
        updateStatusText.Text = YokkoStrings.Get("settings.about.checking_updates");
        _ = runUpdateCheckAsync(updateCheckCancellation.Token);
    }

    public bool TryFocusSearchItem(string itemId) =>
        SettingsSearchScroll.TryFocus(
            SettingsPageKind.About,
            itemId,
            contentRoot: contentRoot);

    private async Task runUpdateCheckAsync(CancellationToken cancellationToken)
    {
        YokkoUpdateCheckResult result =
            await YokkoUpdateChecker.CheckAsync(cancellationToken)
                                    .ConfigureAwait(false);

        Schedule(() =>
        {
            if (disposed)
                return;

            checkingForUpdates = false;
            updateStatusText.Text = result.Message;
        });
    }

    private Drawable createUpdateCard()
    {
        var card = new SettingsStickerCard(
            new Vector2(SettingsChrome.ContentWidth, 86),
            9,
            Color4.White)
        {
            Position = new Vector2(SettingsChrome.ContentX, 278),
        };

        card.SetContent(
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(54),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(25),
                Icon = FontAwesome.Solid.CloudDownloadAlt,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(92, 16),
                Text = YokkoStrings.Get("settings.about.section_version"),
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            updateStatusText = new SpriteText
            {
                Position = new Vector2(92, 48),
                Width = 430,
                Truncate = true,
                Font = HomeTypography.Body(18),
                Colour = SettingsTheme.MutedNavy,
                Text = YokkoStrings.Get("settings.about.update_idle"),
            },
            new SettingsOutlineButton(
                YokkoStrings.Get("settings.about.check_for_updates"),
                FontAwesome.Solid.Sync,
                CheckForUpdates)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
            });

        return card;
    }

    private static Drawable createInformationCard(
        float y,
        IconUsage icon,
        LocalisableString title,
        LocalisableString value,
        Color4 colour)
    {
        var card = new SettingsStickerCard(
            new Vector2(SettingsChrome.ContentWidth, 86),
            9,
            colour)
        {
            Position = new Vector2(SettingsChrome.ContentX, y),
        };

        card.SetContent(
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(54),
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 48,
                Size = new Vector2(25),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(92, 16),
                Text = title,
                Font = HomeTypography.Display(21),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(92, 48),
                Text = value,
                Font = HomeTypography.Body(18),
                Colour = SettingsTheme.MutedNavy,
            });

        return card;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
            updateCheckCancellation.Cancel();
            updateCheckCancellation.Dispose();
        }

        base.Dispose(isDisposing);
    }
}
