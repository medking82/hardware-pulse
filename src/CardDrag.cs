using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

public static class CardDrag {
    public static void Attach(StackPanel panel, Border card, Thumb handle, ScrollViewer scroll, Action changed) {
        Point start = new Point();
        Brush oldBorder = card.BorderBrush;
        bool dragging = false;
        handle.DragStarted += delegate {
            start = Mouse.GetPosition(panel); dragging = true;
            oldBorder = card.BorderBrush;
            card.BorderBrush = Brushes.Aquamarine;
            card.Opacity = .85; Panel.SetZIndex(card, 10);
        };
        handle.DragDelta += delegate {
            if (!dragging) return;
            Point pointer = Mouse.GetPosition(scroll);
            if (pointer.Y < 32) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 16);
            else if (pointer.Y > scroll.ActualHeight - 32) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 16);
            card.RenderTransform = new TranslateTransform(0, Mouse.GetPosition(panel).Y - start.Y);
        };
        handle.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape && dragging) { handle.CancelDrag(); e.Handled = true; }
        };
        handle.DragCompleted += delegate(object sender, DragCompletedEventArgs e) {
            if (!dragging) return;
            dragging = false;
            double y = Mouse.GetPosition(panel).Y;
            card.RenderTransform = Transform.Identity;
            card.Opacity = 1; card.BorderBrush = oldBorder; Panel.SetZIndex(card, 0);
            if (e.Canceled) return;
            int target = 0;
            foreach (UIElement item in panel.Children) {
                if (item == card) continue;
                FrameworkElement other = (FrameworkElement)item;
                double middle = other.TranslatePoint(new Point(0, other.ActualHeight / 2), panel).Y;
                if (y > middle) target++;
            }
            int current = panel.Children.IndexOf(card);
            if (current == target) return;
            panel.Children.RemoveAt(current); panel.Children.Insert(target, card);
            changed(); handle.Focus(); card.BringIntoView();
        };
        handle.PreviewMouseLeftButtonDown += delegate { handle.Focus(); };
    }
}
