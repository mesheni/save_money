using System.Collections;
using System.Collections.Specialized;
using SaveMoney.Helpers;

namespace SaveMoney.Controls;

/// <summary>A wrapping field with a themed selection dialog; selection is by index, never by label.</summary>
public sealed class MonetPicker : Button
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource),
        typeof(IEnumerable), typeof(MonetPicker), propertyChanged: (view, old, value) => ((MonetPicker)view).SourceChanged(old, value));
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(nameof(SelectedIndex),
        typeof(int), typeof(MonetPicker), -1, BindingMode.TwoWay, propertyChanged: (view, _, _) => ((MonetPicker)view).RefreshText());

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    private bool _open;

    public MonetPicker()
    {
        BorderWidth = 0;
        BackgroundColor = Colors.Transparent;
        MinimumHeightRequest = 48;
        Padding = new Thickness(0, 8);
        FontFamily = "InterRegular";
        FontSize = 16;
        LineBreakMode = LineBreakMode.WordWrap;
        SetDynamicResource(TextColorProperty, "OnSurface");
        Clicked += Choose;
        Loaded += (_, _) => { Observe(false); Observe(true); RefreshText(); };
        Unloaded += (_, _) => Observe(false);
    }

    private void SourceChanged(object old, object value)
    {
        if (old is INotifyCollectionChanged previous) previous.CollectionChanged -= ItemsChanged;
        Observe(true);
        RefreshText();
    }

    private void Observe(bool attach)
    {
        if (ItemsSource is not INotifyCollectionChanged source) return;
        if (attach) source.CollectionChanged += ItemsChanged;
        else source.CollectionChanged -= ItemsChanged;
    }

    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshText();
    private string[] Values => ItemsSource?.Cast<object>().Select(v => v.ToString() ?? "").ToArray() ?? [];
    private void RefreshText()
    {
        var items = Values;
        Text = SelectedIndex >= 0 && SelectedIndex < items.Length ? items[SelectedIndex] : "Выбрать";
    }

    private async void Choose(object? sender, EventArgs e)
    {
        if (_open || Values.Length == 0) return;
        _open = true;
        try
        {
            var index = await MonetDialogs.ChooseIndexAsync(SemanticProperties.GetDescription(this) ?? "Выберите значение", Values, SelectedIndex);
            if (index is { } selected) SelectedIndex = selected;
        }
        finally { _open = false; }
    }
}
