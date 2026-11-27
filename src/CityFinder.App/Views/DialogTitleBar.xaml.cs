using System.Windows;
using System.Windows.Controls;

namespace CityFinder.App.Views;

public partial class DialogTitleBar : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(DialogTitleBar),
        new PropertyMetadata(string.Empty));

    public DialogTitleBar()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public event EventHandler? CloseRequested;

    private void Close_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
