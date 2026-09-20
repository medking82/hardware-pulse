using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace HardwarePulse.Desktop;

// Same independent-column placement and shared width policy as WPF SettingsSections.
public sealed class SettingsSections : Panel {
    int columns=1;
    protected override Size MeasureOverride(Size available) {
        var layout=new ColumnLayout(available.Width,350,0,columns);columns=layout.Columns;
        var heights=new double[columns];int i=0;
        foreach(var child in Children.Where(x=>x.IsVisible)) {
            child.Measure(new Size(layout.CellWidth,double.PositiveInfinity));
            heights[i++%columns]+=child.DesiredSize.Height+14;
        }
        return new Size(layout.Width,Math.Max(0,heights.Max()-14));
    }
    protected override Size ArrangeOverride(Size final) {
        double width=Math.Max(1,(final.Width-ColumnLayout.Gap*(columns-1))/columns);
        var heights=new double[columns];int i=0;
        foreach(var child in Children.Where(x=>x.IsVisible)) {
            int column=i++%columns;
            child.Arrange(new Rect(column*(width+ColumnLayout.Gap),heights[column],width,child.DesiredSize.Height));
            heights[column]+=child.DesiredSize.Height+14;
        }
        return final;
    }
    public static Expander Section(UiLanguage language,string name,string title,Control content) {
        var section=language.Set(new Expander{Name=name,IsExpanded=true,Content=content,HorizontalAlignment=HorizontalAlignment.Stretch},title);
        section.Template=new Avalonia.Controls.Templates.FuncControlTemplate<Expander>((item,scope)=>{
            var header=new ToggleButton{Name="SectionHeader",HorizontalAlignment=HorizontalAlignment.Stretch,MinHeight=40,FontWeight=FontWeight.SemiBold,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            header.Bind(ContentControl.ContentProperty,item.GetObservable(HeaderedContentControl.HeaderProperty));
            header.IsChecked=item.IsExpanded;
            header.IsCheckedChanged+=(_,_)=>item.IsExpanded=header.IsChecked==true;
            var body=new Avalonia.Controls.Presenters.ContentPresenter{Margin=new Thickness(8,10,8,8),HorizontalContentAlignment=HorizontalAlignment.Stretch,IsVisible=item.IsExpanded};
            body.Bind(Avalonia.Controls.Presenters.ContentPresenter.ContentProperty,item.GetObservable(ContentControl.ContentProperty));
            item.PropertyChanged+=(_,e)=>{if(e.Property==Expander.IsExpandedProperty){header.IsChecked=item.IsExpanded;body.IsVisible=item.IsExpanded;}};
            header.Template=new Avalonia.Controls.Templates.FuncControlTemplate<ToggleButton>((button,_)=>{
                var label=new Avalonia.Controls.Presenters.ContentPresenter{VerticalAlignment=VerticalAlignment.Center};
                label.Bind(Avalonia.Controls.Presenters.ContentPresenter.ContentProperty,button.GetObservable(ContentControl.ContentProperty));
                var arrow=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M 0 0 L 5 5 L 10 0"),StrokeThickness=1.5,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,RenderTransformOrigin=RelativePoint.Center};
                arrow.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty,button.GetObservable(TemplatedControl.ForegroundProperty));
                void Rotate()=>arrow.RenderTransform=new RotateTransform(button.IsChecked==true?180:0);
                button.IsCheckedChanged+=(_,_)=>Rotate();Rotate();
                var grid=new Grid{ColumnDefinitions=new("*,20")};grid.Children.Add(label);Grid.SetColumn(arrow,1);grid.Children.Add(arrow);
                var plate=new Border{CornerRadius=new CornerRadius(8),Padding=new Thickness(8,6),Child=grid};
                plate.Bind(Border.BackgroundProperty,button.GetObservable(BackgroundProperty));
                plate.Bind(Border.BorderBrushProperty,button.GetObservable(TemplatedControl.BorderBrushProperty));
                plate.Bind(Border.BorderThicknessProperty,button.GetObservable(TemplatedControl.BorderThicknessProperty));return plate;
            });
            header.SetValue(BackgroundProperty,Brushes.Transparent,Avalonia.Data.BindingPriority.Style);
            var hover=new Style(x=>x.OfType<ToggleButton>().Class(":pointerover"));hover.Setters.Add(new Setter(BackgroundProperty,Brush.Parse("#185F8799")));header.Styles.Add(hover);
            var focus=new Style(x=>x.OfType<ToggleButton>().Class(":focus-visible"));focus.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty,Brush.Parse("#BFEAF9")));focus.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty,new Thickness(1)));header.Styles.Add(focus);
            var stack=new StackPanel();stack.Children.Add(header);stack.Children.Add(body);
            return new Border{BorderBrush=Brush.Parse("#486B7C89"),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,0,0,8),Child=stack};
        });
        return section;
    }
}
