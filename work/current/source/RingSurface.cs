using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace OrbitRing {
    // Runtime overlay; LayoutEditor owns editing interactions.
    sealed partial class RingSurface : Canvas {
        public Action<int> Invoked;
        public Action Cancelled;
        public Action BackRequested;
        string stackTitle;
        public string StackTitle { get { return stackTitle; } set { stackTitle=value; RefreshSelection(); } }
        public Action<int> VolumeRequested;
        public Action<int> IconHovered;
        int iconHover=-1;
        Preferences config;
        PlanetMenu menu;
        readonly List<Button> buttons = new List<Button>();
        TextBlock centerTitle, centerDetail;
        Border centerPlate;
        public const double InnerRadius = 80, OuterRadius = 393;
        int hover=-1;
        public RingSurface() {
            Width=410; Height=410; Focusable=false; Background=Brushes.Transparent;
        }
        // Runtime uses a monitor-sized transparent input area, so the outward
        // selection range is independent of the visible ring's bounding box.
        public void AttachPointerArea(UIElement area) {
            area.MouseMove += Move;
            area.MouseLeftButtonUp += Release;
            area.MouseLeave += delegate { ClearHover(); };
            area.MouseRightButtonUp += delegate(object sender,MouseButtonEventArgs e) { if(StackTitle!=null && BackRequested!=null) BackRequested(); else if(Cancelled!=null) Cancelled(); e.Handled=true; };
            area.MouseWheel += delegate(object sender,MouseWheelEventArgs e) {
                UpdatePointer(e.GetPosition(this));
                if(hover>=0 && config.Items[hover].Kind=="volume") {
                    if(VolumeRequested!=null) VolumeRequested(e.Delta);
                    centerTitle.Text=e.Delta>0?"音量 ＋":"音量 −";
                    centerDetail.Text="クリックでミュート"; e.Handled=true;
                }
            };
        }
        public static int Slot(double dx,double dy) { return ((int)Math.Floor((Math.Atan2(dy,dx)*180/Math.PI+90+22.5+360)%360/45))%8; }
        public static int ActionSlot(Point point) {
            double dx=point.X-205,dy=point.Y-205,r=Math.Sqrt(dx*dx+dy*dy);
            return r>=InnerRadius && r<=OuterRadius?Slot(dx,dy):-1;
        }
        public void UpdatePointer(Point point) {
            int next=menu==null?ActionSlot(point):menu.Hit(point); if(next>=0 && config!=null && config.Items[next].Kind=="none") next=-1;
            if(next!=hover) { hover=next; RefreshSelection(); }
            SetIconHover(IconSlot(point));
        }
        public int IconSlot(Point point) {
            if(config==null) return -1;
            if(menu!=null) return menu.IconHit(point);
            for(int i=0;i<8;i++) if(config.Items[i].Kind!="none" && (Position(i)-point).Length<=config.ButtonSize/2) return i;
            return -1;
        }
        void SetIconHover(int index) { if(iconHover==index) return; iconHover=index; if(IconHovered!=null) IconHovered(index); }
        void ClearHover() { hover=-1; SetIconHover(-1); RefreshSelection(); }
        public static Point Position(int index) { double a=(index*45-90)*Math.PI/180; return new Point(205+Math.Cos(a)*131,205+Math.Sin(a)*131); }
        public void SetScale(double scale) { LayoutTransform=new ScaleTransform(scale,scale); }
        public Point IconPosition(int index) { return menu==null?Position(index):menu.Targets[index].Position; }
        public double IconSize(int index) { return menu==null?config.ButtonSize:menu.Targets[index].Size; }
        public int IconNumber(int index) { return menu==null?index+1:menu.Targets[index].Slot+1; }
        public void Bind(Preferences value,PlanetMenu navigation=null) {
            config=value; menu=navigation; InitializeOpeningAppearance(); hover=-1; SetIconHover(-1); Children.Clear(); buttons.Clear();
            if(!config.Appearance.TextOnly) MaterialIcons.Prepare(config.Items.Select(ActionIcons.EffectiveKey));
            if(menu!=null) foreach(PlanetLayer layer in menu.Layers) if(layer.Arc) DrawArc(layer);
            Ellipse plate=new Ellipse { Width=378, Height=378, Fill=openingPlateFill, Stroke=openingPlateEdge, StrokeThickness=1,
                Effect=new DropShadowEffect { Color=Colors.Black, BlurRadius=18, ShadowDepth=4, Opacity=.25 } };
            Put(plate,16,16);
            Put(new Ellipse { Width=262, Height=262, Stroke=Ui.Brush("#35404C"), StrokeThickness=1 },74,74);
            centerPlate=CenterBody(config.Appearance,out centerTitle,out centerDetail); centerTitle.Text="操作を選択"; centerDetail.Text="中央でキャンセル";
            Put(centerPlate,132,132);
            for(int i=0;i<config.Items.Count;i++) {
                double size=IconSize(i); Button b=Bubble(config.Items[i],IconNumber(i)-1,size,config.Appearance.TextOnly); b.IsHitTestVisible=false; if(config.Items[i].Kind=="none") b.Visibility=Visibility.Hidden;
                Point p=IconPosition(i); Put(b,p.X-size/2,p.Y-size/2); buttons.Add(b);
            }
            RefreshSelection();
        }
        void DrawArc(PlanetLayer layer) {
            double half=Math.Max(.12,(Math.Max(1,layer.Targets.Count)-1)*layer.Spacing/2+.07);
            Point start=PlanetMenu.Polar(layer.Radius,layer.Angle-half),end=PlanetMenu.Polar(layer.Radius,layer.Angle+half);
            StreamGeometry curve=new StreamGeometry(); using(StreamGeometryContext context=curve.Open()) { context.BeginFigure(start,false,false); context.ArcTo(end,new Size(layer.Radius,layer.Radius),0,false,SweepDirection.Clockwise,true,false); } curve.Freeze();
            foreach(bool edge in new[]{true,false}) Children.Add(new Path { Data=curve,Stroke=edge?openingPlateEdge:openingPlateFill,StrokeThickness=layer.Size+(edge?20:18),StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false });
            if(layer.Targets.Count==0) {
                TextBlock empty=Ui.Text("空のスタック",11,Ui.Muted); empty.Width=100; empty.TextAlignment=TextAlignment.Center; Point at=PlanetMenu.Polar(layer.Radius,layer.Angle); Put(empty,at.X-50,at.Y-8);
            }
        }
        void Put(UIElement element,double x,double y) { SetLeft(element,x); SetTop(element,y); Children.Add(element); }
        internal static Border CenterBody(AppearancePreferences appearance,out TextBlock title,out TextBlock detail) {
            StackPanel middle=new StackPanel { Width=130,VerticalAlignment=VerticalAlignment.Center };
            TextBlock mark=Ui.Text("P L A N E T",11,appearance.Accent); mark.TextAlignment=TextAlignment.Center; mark.Margin=new Thickness(0,0,0,9); middle.Children.Add(mark);
            title=Ui.Text("",14,Ui.Ink); title.TextAlignment=TextAlignment.Center; title.FontWeight=FontWeights.SemiBold; middle.Children.Add(title);
            detail=Ui.Text("",10,Ui.Muted); detail.TextAlignment=TextAlignment.Center; detail.Margin=new Thickness(0,6,0,0); middle.Children.Add(detail);
            return new Border { Width=146,Height=146,CornerRadius=new CornerRadius(73),Background=Ui.Bg,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Child=middle };
        }
        internal static TextBlock TextLabel(string label,double size) { return new TextBlock { Text=label,FontSize=Math.Min(13,Math.Max(10,size/6)),FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center,MaxWidth=size*.78,MaxHeight=size*.65,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center }; }
        public static Button Bubble(ActionItem item,int index,double size,bool textOnly=false) {
            StackPanel body=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
            FrameworkElement glyph=ActionIcons.UsesText(item,textOnly)?TextLabel(ActionIcons.Caption(item),size):ActionIcons.Visual(item,22*size/76); glyph.HorizontalAlignment=HorizontalAlignment.Center; body.Children.Add(glyph);
            Button b=new Button { Width=size, Height=size, Content=body, Background=Ui.Panel, BorderBrush=Ui.Line, BorderThickness=new Thickness(1), Focusable=false, Cursor=Cursors.Hand, ToolTip=item.Label+(item.Kind=="shortcut"?" · "+item.Value:item.Kind=="volume"?" · ホイールで調整":"") };
            FrameworkElementFactory border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(size/2)); border.SetValue(Border.BorderThicknessProperty,new Thickness(1.5));
            border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            FrameworkElementFactory presenter=new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center); border.AppendChild(presenter);
            b.Template=new ControlTemplate(typeof(Button)) { VisualTree=border };
            System.Windows.Automation.AutomationProperties.SetName(b,(index+1)+" "+item.Label); return b;
        }
        void Move(object sender,MouseEventArgs e) { UpdatePointer(e.GetPosition(this)); }
        void Release(object sender,MouseButtonEventArgs e) {
            if(e.Handled) return;
            // Use the release position, never a selection left over from MouseMove.
            UpdatePointer(e.GetPosition(this)); e.Handled=true;
            if(hover>=0 && Invoked!=null) Invoked(hover); else if(StackTitle!=null && (e.GetPosition(this)-new Point(205,205)).Length<InnerRadius && BackRequested!=null) BackRequested(); else if(Cancelled!=null) Cancelled();
        }
        void RefreshSelection() {
            if(centerTitle==null || config==null) return;
            int active=hover;
            for(int i=0;i<buttons.Count;i++) {
                bool highlighted=i==active || (menu!=null && menu.IsExpanded(i));
                buttons[i].Background=highlighted?selectedButtonFill:openingButtonFill; buttons[i].BorderBrush=highlighted?config.Appearance.Accent:openingButtonEdge;
            }
            centerPlate.Background=active<0?Ui.Brush("#422733"):Ui.Bg;
            centerPlate.BorderBrush=active<0?Ui.Brush("#F0809B"):Ui.Line;
            centerTitle.Foreground=active<0?Ui.Brush("#FFA1B9"):Ui.Ink;
            centerTitle.Text=active<0?(StackTitle!=null?"← 戻る":"× キャンセル"):config.Items[active].Label;
            centerDetail.Text=active<0?(StackTitle??"クリックで閉じる"):config.Items[active].Kind=="shortcut"?config.Items[active].Value:config.Items[active].Kind=="hold"?"押している間保持":config.Items[active].Kind=="macro"?"クリックで順に実行":config.Items[active].Kind=="toggle"?config.Items[active].Value:config.Items[active].Kind=="volume"?"ホイールで調整":"クリックで開く";
        }
        public void ShowPreviewSelection(int index) { centerTitle.Text=config.Items[index].Label; centerDetail.Text="プレビュー · 実行なし"; }
    }
}
