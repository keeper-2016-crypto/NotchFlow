using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NotchFlow.Views;

public partial class IslandWindow
{
    Grid? confirmation;
    public bool HasInteractiveOverlay => interactions.IsActive;

    public void ConfirmDeletion(string title, Action delete, string message="Это действие нельзя отменить.", string acceptText="Удалить")
    {
        if (confirmation != null) return;
        SetExpanded(true);
        collapse.Stop();
        hover.Stop();
        var host = ClipRoot;
        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(108, 0, 0, 0)) };
        var card = new Border
        {
            Width = 276, Padding = new Thickness(16), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), BorderBrush = SystemPane.Accent,
            Background = new SolidColorBrush(Color.FromRgb(17, 17, 19)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 18 });
        panel.Children.Add(new TextBlock
        {
            Text = message, TextWrapping=TextWrapping.Wrap, Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 12, 0, 16)
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Отмена" };
        var accept = new Button { Content = acceptText, Background = new SolidColorBrush(Color.FromRgb(180, 35, 42)) };
        void Close()
        {
            host.Children.Remove(overlay);
            confirmation = null;
            interactions.Set(overlay,false);
            if (!closing && settings.CollapseOnLeave && !pinned && !Capsule.IsMouseOver) collapse.Start();
        }
        cancel.Click += (_, _) => Close();
        accept.Click += (_, _) => { Close(); delete(); };
        overlay.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        card.Child = panel;
        overlay.Children.Add(card);
        confirmation = overlay;
        interactions.Set(overlay,true);
        host.Children.Add(overlay);
        cancel.Focus();
    }
}
