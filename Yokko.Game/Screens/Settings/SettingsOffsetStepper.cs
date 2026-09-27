using System;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class SettingsOffsetStepper : CompositeDrawable
{
    private readonly Bindable<double> offset;
    private readonly OffsetInput input;
    public override bool AcceptsFocus => true;

    public SettingsOffsetStepper(Bindable<double> offset)
    {
        this.offset = offset;
        Size = new Vector2(598, 50);
        InternalChildren = new Drawable[]
        {
            step("−10", -10, 0), step("−1", -1, 74),
            input = new OffsetInput(offset) { Position = new Vector2(148, 4), Size = new Vector2(138, 42) },
            new SettingsReadableText { Position = new Vector2(296, 14), Text = "ms", Font = HomeTypography.Body(16), Colour = HomeControlColours.Navy },
            step("+1", 1, 334), step("+10", 10, 408),
            new GameplayCompactButton(YokkoStrings.Get("settings.offset.zero"), () => SetValue(0), 112)
            { Position = new Vector2(486, 4) },
        };
    }

    private Drawable step(string text, double delta, float x) => new GameplayCompactButton(text,
        () => SetValue(offset.Value + delta), 66) { Position = new Vector2(x, 4) };
    private void SetValue(double value)
    {
        offset.Value = Math.Clamp(Math.Round(value), -200, 200);
        input.Refresh();
    }

    internal static bool TryParseOffset(string text, out double value) =>
        double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
            CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value is >= -200 and <= 200;

    internal static double AdjustForKey(double value, Key key, bool largeStep = false)
    {
        double step = largeStep ? 10 : 1;
        return key switch
        {
            Key.Left or Key.Down => Math.Clamp(Math.Round(value - step), -200, 200),
            Key.Right or Key.Up => Math.Clamp(Math.Round(value + step), -200, 200),
            Key.Home => 0,
            _ => value,
        };
    }
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (input.HasFocus) return false;
        if (e.Key is Key.Left or Key.Down or Key.Right or Key.Up or Key.Home)
        {
            SetValue(AdjustForKey(offset.Value, e.Key, e.ShiftPressed));
            return true;
        }
        return base.OnKeyDown(e);
    }

    private partial class OffsetInput : BasicTextBox
    {
        private readonly Bindable<double> offset;
        public OffsetInput(Bindable<double> offset)
        {
            this.offset = offset;
            LengthLimit = 5;
            FontSize = 18;
            Masking = true;
            CornerRadius = 7;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;
            BackgroundFocused = SettingsTheme.PaleCyan;
            BackgroundUnfocused = Color4.White;
            offset.ValueChanged += changed;
            Refresh();
        }
        public void Refresh() => Text = offset.Value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        private void changed(ValueChangedEvent<double> _) { if (!HasFocus) Refresh(); }
        private bool commit()
        {
            if (!TryParseOffset(Text, out double value)) { BorderColour = HomeControlColours.Pink; return false; }
            offset.Value = value;
            BorderColour = HomeControlColours.Navy;
            Refresh();
            return true;
        }
        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key is Key.Enter or Key.KeypadEnter)
            {
                if (commit()) GetContainingFocusManager()?.ChangeFocus(null);
                return true;
            }
            if (e.Key == Key.Escape)
            {
                Refresh();
                GetContainingFocusManager()?.ChangeFocus(null);
                return true;
            }
            return base.OnKeyDown(e);
        }
        protected override void OnFocusLost(FocusLostEvent e)
        {
            if (!commit()) Refresh();
            BorderColour = HomeControlColours.Navy;
            base.OnFocusLost(e);
        }
        protected override Drawable GetDrawableCharacter(char c) => new SettingsReadableText
        { Text = c.ToString(), Font = HomeTypography.SearchInput(18), Colour = HomeControlColours.Navy };
        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing) offset.ValueChanged -= changed;
            base.Dispose(isDisposing);
        }
    }
}
