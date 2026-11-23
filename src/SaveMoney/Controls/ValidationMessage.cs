using SaveMoney.Helpers;

namespace SaveMoney.Controls;

/// <summary>Inline error that reveals its field when Save is below the fold.</summary>
public sealed class ValidationMessage : Label
{
    public ValidationMessage()
    {
        FontFamily = "InterRegular";
        FontSize = 14;
        LineBreakMode = LineBreakMode.WordWrap;
        IsVisible = false;
        SetDynamicResource(TextColorProperty, "Error");
        SemanticProperties.SetHint(this, "Ошибка ввода");
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(Text)) return;
            var wasVisible = IsVisible;
            IsVisible = !string.IsNullOrEmpty(Text);
            if (!IsVisible) return;
            Dispatcher.Dispatch(Reveal);
            // Новая ошибка проявляется, повторная — покачивается, чтобы её заметили.
            if (Handler is not null) _ = wasVisible ? this.ShakeAsync(6) : this.RevealAsync();
        };
    }

    private async void Reveal()
    {
        if (!IsVisible) return;
        Element? parent = Parent;
        while (parent is not null && parent is not ScrollView) parent = parent.Parent;
        if (parent is ScrollView scroll)
            await scroll.ScrollToAsync(this, ScrollToPosition.Center, Motion.AnimationsEnabled);
        SemanticScreenReader.Default.Announce(Text);
    }
}
