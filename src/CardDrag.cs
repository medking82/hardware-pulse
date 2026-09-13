using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

public static class CardDrag {
    public sealed class Gesture {
        readonly StackPanel panel;
        readonly Border card;
        readonly Action changed;
        readonly bool motion;
        Brush oldBorder;
        double grab;
        int target;
        public bool Active { get; private set; }
        public Gesture(StackPanel panel, Border card, Action changed, bool motion) {
            this.panel = panel; this.card = card; this.changed = changed; this.motion = motion;
            foreach (FrameworkElement item in panel.Children) Translation(item);
        }
        static TranslateTransform Translation(FrameworkElement item) {
            var group = item.RenderTransform as TransformGroup;
            if (group == null) {
                group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1, 1)); group.Children.Add(new TranslateTransform());
                item.RenderTransform = group; item.RenderTransformOrigin = new Point(.5, .5);
            }
            return (TranslateTransform)group.Children[1];
        }
        static double Top(FrameworkElement item) { return LayoutInformation.GetLayoutSlot(item).Top + item.Margin.Top; }
        void Animate(Animatable item, DependencyProperty property, double targetValue) {
            double current = (double)item.GetValue(property);
            item.BeginAnimation(property, null); item.SetValue(property, targetValue);
            if (!motion || !SystemParameters.ClientAreaAnimation) return;
            // Strong ease-out, interruptible from the currently presented value.
            var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(220), FillBehavior = FillBehavior.Stop };
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(current, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            frames.KeyFrames.Add(new SplineDoubleKeyFrame(targetValue, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)), new KeySpline(.23, 1, .32, 1)));
            item.BeginAnimation(property, frames, HandoffBehavior.SnapshotAndReplace);
        }
        void Scale(double value) {
            var scale = (ScaleTransform)((TransformGroup)card.RenderTransform).Children[0];
            Animate(scale, ScaleTransform.ScaleXProperty, value); Animate(scale, ScaleTransform.ScaleYProperty, value);
        }
        public void Begin(double pointerY) {
            panel.UpdateLayout();
            var shift = Translation(card);
            grab = pointerY - Top(card) - shift.Y;
            double current = shift.Y;
            shift.BeginAnimation(TranslateTransform.YProperty, null); shift.Y = current;
            Active = true; target = panel.Children.IndexOf(card);
            oldBorder = card.BorderBrush; card.BorderBrush = Brushes.Aquamarine;
            foreach (FrameworkElement item in panel.Children) Panel.SetZIndex(item, item == card ? 10 : 0);
            Scale(motion && SystemParameters.ClientAreaAnimation ? 1.015 : 1);
        }
        public void Move(double pointerY) {
            if (!Active) return;
            double y = pointerY - grab;
            Translation(card).Y = y - Top(card);
            int next = 0;
            foreach (FrameworkElement other in panel.Children)
                if (other != card && y + card.ActualHeight / 2 > Top(other) + other.ActualHeight / 2) next++;
            if (next == target) return;
            target = next;
            var order = new List<FrameworkElement>();
            foreach (FrameworkElement other in panel.Children) if (other != card) order.Add(other);
            order.Insert(target, card);
            double top = 0;
            foreach (FrameworkElement other in order) {
                if (other != card) Animate(Translation(other), TranslateTransform.YProperty, top + other.Margin.Top - Top(other));
                top += LayoutInformation.GetLayoutSlot(other).Height;
            }
        }
        public void Complete(bool canceled) {
            if (!Active) return;
            Active = false;
            var positions = new Dictionary<FrameworkElement, double>();
            foreach (FrameworkElement item in panel.Children) positions[item] = Top(item) + Translation(item).Y;
            int original = panel.Children.IndexOf(card);
            bool reordered = !canceled && original != target;
            if (reordered) { panel.Children.RemoveAt(original); panel.Children.Insert(target, card); }
            panel.UpdateLayout();
            foreach (FrameworkElement item in panel.Children) {
                var shift = Translation(item);
                shift.BeginAnimation(TranslateTransform.YProperty, null); shift.Y = positions[item] - Top(item);
                Animate(shift, TranslateTransform.YProperty, 0);
            }
            Scale(1); card.BorderBrush = oldBorder;
            if (reordered) changed();
        }
    }
    public static void Attach(StackPanel panel, Border card, Thumb handle, ScrollViewer scroll, Action changed) {
        var gesture = new Gesture(panel, card, changed, true);
        handle.DragStarted += delegate { gesture.Begin(Mouse.GetPosition(panel).Y); };
        handle.DragDelta += delegate {
            if (!gesture.Active) return;
            Point pointer = Mouse.GetPosition(scroll);
            if (pointer.Y < 32) scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 16);
            else if (pointer.Y > scroll.ActualHeight - 32) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 16);
            scroll.UpdateLayout(); gesture.Move(Mouse.GetPosition(panel).Y);
        };
        handle.PreviewKeyDown += delegate(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape && gesture.Active) { handle.CancelDrag(); e.Handled = true; }
        };
        handle.DragCompleted += delegate(object sender, DragCompletedEventArgs e) {
            gesture.Complete(e.Canceled); handle.Focus();
        };
        handle.PreviewMouseLeftButtonDown += delegate { handle.Focus(); };
    }
}
