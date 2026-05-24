using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;

namespace H2CursorRouter.App;

internal sealed class AppDialogWindow : Window
{
    public AppDialogWindow(string title, UIElement body)
    {
        Title = title;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Background = TryFindResource("AppBackgroundBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White;
        Foreground = TryFindResource("TextBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Black;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI");
        FontSize = 13;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 40,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        Owner = WpfApplication.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive) ?? WpfApplication.Current?.MainWindow;
        Content = CreateShell(title, body);
    }

    private UIElement CreateShell(string title, UIElement body)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var caption = new Border
        {
            Background = TryFindResource("ShellBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Black
        };
        caption.MouseLeftButtonDown += (_, args) =>
        {
            if (args.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
        Grid.SetRow(caption, 0);

        var captionGrid = new Grid();
        captionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        captionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        captionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        captionGrid.Children.Add(new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(3),
            Background = TryFindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Teal,
            Margin = new Thickness(18, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        var titleText = new TextBlock
        {
            Text = title,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 1);
        captionGrid.Children.Add(titleText);

        var closeButton = new WpfButton
        {
            Content = "×",
            Style = TryFindResource("WindowCloseButtonStyle") as Style
        };
        closeButton.Click += (_, _) => DialogResult = false;
        WindowChrome.SetIsHitTestVisibleInChrome(closeButton, true);
        Grid.SetColumn(closeButton, 2);
        captionGrid.Children.Add(closeButton);

        caption.Child = captionGrid;
        root.Children.Add(caption);

        var contentBorder = new Border
        {
            Background = Background,
            Padding = new Thickness(20)
        };
        Grid.SetRow(contentBorder, 1);
        contentBorder.Child = body;
        root.Children.Add(contentBorder);

        return root;
    }
}
