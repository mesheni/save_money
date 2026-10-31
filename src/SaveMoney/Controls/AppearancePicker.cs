using System.Collections;
using Microsoft.Maui.Layouts;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace SaveMoney.Controls;

/// <summary>Shared wrapping, accessible selector for account colors and category icons.</summary>
public sealed class AppearancePicker : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource),
        typeof(IEnumerable), typeof(AppearancePicker), propertyChanged: (view, _, value) =>
            BindableLayout.SetItemsSource(((AppearancePicker)view)._items, (IEnumerable?)value));
    public static readonly BindableProperty SelectionCommandProperty = BindableProperty.Create(nameof(SelectionCommand),
        typeof(ICommand), typeof(AppearancePicker));
    public static readonly BindableProperty IsColorPickerProperty = BindableProperty.Create(nameof(IsColorPicker),
        typeof(bool), typeof(AppearancePicker), false);

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public ICommand? SelectionCommand { get => (ICommand?)GetValue(SelectionCommandProperty); set => SetValue(SelectionCommandProperty, value); }
    public bool IsColorPicker { get => (bool)GetValue(IsColorPickerProperty); set => SetValue(IsColorPickerProperty, value); }
    private readonly FlexLayout _items = new() { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Start, AlignContent = FlexAlignContent.Start };

    public AppearancePicker()
    {
        Content = _items;
        BindableLayout.SetItemTemplate(_items, new DataTemplate(CreateChoice));
    }

    private object CreateChoice()
    {
        var border = new Border
        {
            WidthRequest = 56, HeightRequest = 64, MinimumHeightRequest = 56, Margin = new Thickness(0, 0, 8, 8), Padding = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 }, StrokeThickness = 2,
        };
        border.SetDynamicResource(Border.StrokeProperty, "OutlineVariant");
        if (IsColorPicker) border.SetBinding(BackgroundColorProperty, "Hex");
        else border.SetDynamicResource(BackgroundColorProperty, "SecondaryContainer");
        var selected = new DataTrigger(typeof(Border)) { Binding = new Binding("IsSelected"), Value = true };
        selected.Setters.Add(new Setter { Property = Border.StrokeThicknessProperty, Value = 4.0 });
        border.Triggers.Add(selected);
        var grid = new Grid();
        var button = new Button
        {
            BackgroundColor = Colors.Transparent, BorderWidth = 0, Padding = 0,
            MinimumHeightRequest = 56, MinimumWidthRequest = 56, FontFamily = "MaterialSymbolsRounded", FontSize = 24,
        };
        button.SetDynamicResource(Button.TextColorProperty, "OnSecondaryContainer");
        button.SetBinding(Button.CommandProperty, new Binding(nameof(SelectionCommand), source: this));
        button.SetBinding(Button.CommandParameterProperty, ".");
        button.SetBinding(SemanticProperties.DescriptionProperty, "AccessibleName");
        SemanticProperties.SetHint(button, IsColorPicker ? "Выбрать цвет счёта" : "Выбрать значок категории");
        if (!IsColorPicker)
            button.SetBinding(Button.TextProperty, new Binding("Key", converter: new IconKeyConverter()));
        grid.Add(button);
        var check = new Label
        {
            Text = "✓", FontSize = 16, FontFamily = "InterBold", HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center, InputTransparent = true,
        };
        check.SetDynamicResource(Label.TextColorProperty, "OnPrimary");
        var badge = new Border
        {
            Content = check, WidthRequest = 24, HeightRequest = 24, Padding = 0, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, InputTransparent = true,
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start,
        };
        badge.SetDynamicResource(BackgroundColorProperty, "Primary");
        badge.SetBinding(IsVisibleProperty, "IsSelected");
        grid.Add(badge);
        border.Content = grid;
        return border;
    }
}
