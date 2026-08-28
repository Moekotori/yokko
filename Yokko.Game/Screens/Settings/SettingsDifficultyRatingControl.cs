using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using Yokko.Core.Difficulty;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsDifficultyRatingControl
{
    internal static Drawable Create(
        Bindable<ManiaDifficultyRatingMode> difficultyRatingMode,
        List<SettingsSegmentedChoiceButton> buttons,
        float buttonWidth = SettingsChrome.ControlWidth / 2f)
    {
        buttons.Clear();

        var options = new[]
        {
            (
                ManiaDifficultyRatingMode.EtternaMsd,
                YokkoStrings.Get("settings.gameplay.difficulty_rating.etterna"),
                FontAwesome.Solid.ChartLine),
            (
                ManiaDifficultyRatingMode.RebirthStars,
                YokkoStrings.Get("settings.gameplay.difficulty_rating.rebirth"),
                FontAwesome.Solid.Star),
        };

        foreach ((ManiaDifficultyRatingMode mode,
                     LocalisableString label,
                     IconUsage icon) in options)
        {
            ManiaDifficultyRatingMode capturedMode = mode;
            buttons.Add(new SettingsSegmentedChoiceButton(
                label,
                icon,
                () => difficultyRatingMode.Value = capturedMode,
                buttonWidth)
            {
                Value = mode,
            });
        }

        return SettingsChrome.CreateSegmentedControl(buttons);
    }

    internal static void RefreshSelection(
        IEnumerable<SettingsSegmentedChoiceButton> buttons,
        ManiaDifficultyRatingMode current)
    {
        foreach (SettingsSegmentedChoiceButton button in buttons)
        {
            button.SetSelected(
                button.Value is ManiaDifficultyRatingMode mode
                && mode == current);
        }
    }
}
