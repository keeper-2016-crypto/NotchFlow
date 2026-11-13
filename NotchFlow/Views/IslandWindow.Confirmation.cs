using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NotchFlow.Views;

public partial class IslandWindow
{
    Grid? confirmation;
    public bool HasInteractiveOverlay => interactions.IsActive;

    public void ConfirmDeletion(string title, Action delete)
    {
        if (confirmation != null) return;
        SetExpanded(true);
        collapse.Stop();
        hover.Stop();
        var host = (Grid)Content;
        var overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)) };
        var card = new Border
        {
            Width = 300, Padding = new Thickness(20), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromRgb(17, 17, 19)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 18 });
        panel.Children.Add(new TextBlock
        {
            Text = "This action cannot be undone.", Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 12, 0, 16)
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel" };
        var accept = new Button { Content = "Delete", Background = new SolidColorBrush(Color.FromRgb(180, 35, 42)) };
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
