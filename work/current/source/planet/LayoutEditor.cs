using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OrbitRing {
    // Only drags started inside this process can change the layout.
    static class EditorDrag {
        public static bool Active { get; private set; }
        public static ActionItem Item { get; private set; }
        public static void Run(UIElement source,ActionItem item) {
            if(Active) return;
            Active=true; Item=item;
            try { DragDrop.DoDragDrop(source,new DataObject(DataFormats.UnicodeText,"OrbitRing"),DragDropEffects.Copy); }
            finally { Active=false; Item=null; }
        }
    }
    sealed class LayoutEditor : Canvas {
        public Action<int[],int> SelectionChanged,Entered,Erased;
        public Action<int[],int,int[],int> Moved;
        public Action<int[],int,ActionItem> Assigned;
        public Action<int[],int,ActionItem,int> Inserted;
        EditorTarget insertion;
        Border insertionMarker;
        const double EditorScale=1.5, EditorButtonSize=76;
        readonly List<Button> buttons=new List<Button>();
        PlanetEditorLayout layout;
        IList<ActionItem> rootItems;
        string moveHint;
        AppearancePreferences appearance=new AppearancePreferences();
        Point start;
        Button pressed;
        EditorTarget movingItem;
        bool moving;
        Vector offset;
        int target=-1;
        TextBlock centerTitle,centerDetail;
        public LayoutEditor() {
            Width=860; Height=640; Background=Brushes.Transparent; AllowDrop=true;
            DragOver+=Over; Drop+=Dropped; DragLeave+=delegate { target=-1; insertion=null; Highlight(); };
            PreviewMouseMove+=delegate(object sender,MouseEventArgs e) { if(pressed!=null && UpdateMove(e.GetPosition(this),e.LeftButton==MouseButtonState.Pressed)) e.Handled=true; };
            PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) { if(FinishMove(e.GetPosition(this))) e.Handled=true; };
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(moving && e.Key==Key.Escape) { CancelMove(true); e.Handled=true; } };
        }
        Point Project(Point point) { return new Point(430+(point.X-205)*EditorScale+offset.X,320+(point.Y-205)*EditorScale+offset.Y); }
        public Point Position(int index) { return Project(PlanetMenu.Polar(131,(index*45-90)*Math.PI/180)); }
        public int DropSlot(Point point) {
            if(layout==null) return -1;
            int nearest=-1; double distance=Double.MaxValue;
            for(int i=layout.Targets.Count-1;i>=0;i--) { double current=(Project(layout.Targets[i].Position)-point).Length; if(current<=layout.Targets[i].Size*EditorScale/2+10 && current<distance) { nearest=i; distance=current; } }
            if(nearest>=0) return nearest;
            foreach(Button label in Children.OfType<Button>().Where(b=>b.Tag is EditorTarget)) {
                Rect bounds=new Rect(GetLeft(label),GetTop(label),label.DesiredSize.Width,label.DesiredSize.Height);
                if(bounds.Contains(point)) return layout.Targets.IndexOf((EditorTarget)label.Tag);
            }
            return -1;
        }
        void Put(UIElement element,double x,double y) { SetLeft(element,x); SetTop(element,y); Children.Add(element); }
        public void Bind(Preferences value,int selection,int[] path=null) {
            CancelMove(true);
            rootItems=value.Items; appearance=value.Appearance; target=-1; insertion=null; insertionMarker=null; offset=new Vector(); Children.Clear(); buttons.Clear();
            layout=new PlanetEditorLayout(value.Items,path??new int[0],selection,value.Appearance.StackGap,value.Appearance.EvenSpacing);
            if(!appearance.TextOnly) MaterialIcons.Prepare(layout.Targets.Select(item=>ActionIcons.EffectiveKey(item.Item)));
            Rect bounds=new Rect(0,0,860,640);
            foreach(EditorTarget item in layout.Targets.Where(t=>layout.Menu.Layers.Any(l=>l.Arc && l.Depth==t.Depth))) {
                Point p=Project(item.Position); double half=item.Size*EditorScale/2;
                bounds.Union(new Rect(p.X-half-12,p.Y-half-12,half*2+24,half*2+24));
                Vector direction=item.Position-new Point(205,205); direction.Normalize();
                Point label=p+direction*(half+14+Math.Abs(direction.X)*73+Math.Abs(direction.Y)*18);
                bounds.Union(new Rect(label.X-85,label.Y-30,170,60));
            }
            offset=new Vector(-bounds.Left,-bounds.Top); Width=bounds.Width; Height=bounds.Height;
            Point center=Project(new Point(205,205)); double scale=EditorScale;
            Put(new Ellipse { Width=378*scale,Height=378*scale,Fill=Ui.Brush("#F2181E26"),Stroke=Ui.Line,StrokeThickness=1 },center.X-189*scale,center.Y-189*scale);
            Put(new Ellipse { Width=262*scale,Height=262*scale,Stroke=Ui.Brush("#35404C"),StrokeThickness=1 },center.X-131*scale,center.Y-131*scale);
            foreach(PlanetLayer layer in layout.Menu.Layers) if(layer.Arc) DrawArc(layer);
            StackPanel middle=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
            TextBlock mark=Ui.Text("P L A N E T",11*scale,appearance.Accent); mark.TextAlignment=TextAlignment.Center; mark.Margin=new Thickness(0,0,0,9*scale); middle.Children.Add(mark);
            centerTitle=Ui.Text("配置を編集",14*scale,Ui.Ink); centerTitle.TextAlignment=TextAlignment.Center; centerTitle.FontWeight=FontWeights.SemiBold; middle.Children.Add(centerTitle);
            centerDetail=Ui.Text("ドラッグで並べ替え",10*scale,Ui.Muted); centerDetail.TextAlignment=TextAlignment.Center; centerDetail.MaxWidth=190; centerDetail.TextTrimming=TextTrimming.CharacterEllipsis; centerDetail.Margin=new Thickness(0,6*scale,0,0); middle.Children.Add(centerDetail);
            Put(new Border { Width=146*scale,Height=146*scale,CornerRadius=new CornerRadius(73*scale),Background=Ui.Bg,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Child=middle,IsHitTestVisible=false },center.X-73*scale,center.Y-73*scale);
            foreach(EditorTarget item in layout.Targets) AddButton(item);
            List<Rect> labelBounds=new List<Rect>();
            foreach(EditorTarget item in layout.Targets.Where(t=>!ActionIcons.UsesText(t.Item,appearance.TextOnly)).OrderByDescending(t=>t.Depth)) AddLabel(item,labelBounds);
            Rect complete=new Rect(0,0,Width,Height); foreach(Rect label in labelBounds) { Rect padded=label; padded.Inflate(10,10); complete.Union(padded); }
            if(complete.Left<0 || complete.Top<0) {
                Vector shift=new Vector(-complete.Left,-complete.Top); offset+=shift;
                foreach(UIElement child in Children) { double left=GetLeft(child),top=GetTop(child); SetLeft(child,(Double.IsNaN(left)?0:left)+shift.X); SetTop(child,(Double.IsNaN(top)?0:top)+shift.Y); }
            }
            Width=complete.Width; Height=complete.Height;
            insertionMarker=new Border { Width=38,Height=38,CornerRadius=new CornerRadius(19),Background=appearance.Selected,BorderBrush=appearance.Accent,BorderThickness=new Thickness(2),IsHitTestVisible=false,Visibility=Visibility.Collapsed,Child=new TextBlock { Text="＋",FontSize=24,Foreground=Ui.Ink,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center } };
            Children.Add(insertionMarker);
            Highlight();
        }
        void DrawArc(PlanetLayer layer) {
            EditorTarget[] items=layout.Targets.Where(t=>t.Depth==layer.Depth && Math.Abs(t.Radius-layer.Radius)<.001).ToArray(); if(items.Length==0) return;
            double first=items[0].Angle-layer.Spacing*.5,last=items[items.Length-1].Angle+layer.Spacing*.5,radius=layer.Radius*EditorScale;
            StreamGeometry geometry=new StreamGeometry();
            using(StreamGeometryContext drawing=geometry.Open()) { drawing.BeginFigure(Project(PlanetMenu.Polar(layer.Radius,first)),false,false); drawing.ArcTo(Project(PlanetMenu.Polar(layer.Radius,last)),new Size(radius,radius),0,last-first>Math.PI,SweepDirection.Clockwise,true,false); }
            geometry.Freeze(); Children.Add(new System.Windows.Shapes.Path { Data=geometry,Stroke=Ui.Brush("#C0181E26"),StrokeThickness=(layer.Size+20)*EditorScale,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,IsHitTestVisible=false });
            Children.Add(new System.Windows.Shapes.Path { Data=geometry,Stroke=Ui.Brush("#526C62"),StrokeThickness=1.5,IsHitTestVisible=false });
        }
        void AddButton(EditorTarget item) {
            Point p=Project(item.Position); double size=item.Size*EditorScale;
            Button button=EditorUi.Button("",delegate { if(SelectionChanged!=null) SelectionChanged(item.Path,item.Slot); },false);
            button.Width=size; button.Height=size; button.Padding=new Thickness(0); button.Margin=new Thickness(0); button.Foreground=Ui.Ink;
            button.ToolTip=item.Item.Label; button.Content=ActionIcons.UsesText(item.Item,appearance.TextOnly)?RingSurface.TextLabel(item.Item.Kind=="none"?"＋":ActionIcons.Caption(item.Item),size):ActionIcons.Visual(item.Item,22*size/76);
            BindEraseMenu(button,item);
            FrameworkElementFactory edge=new FrameworkElementFactory(typeof(Border)); edge.SetValue(Border.CornerRadiusProperty,new CornerRadius(size/2)); edge.SetValue(Border.BorderThicknessProperty,new Thickness(1.5));
            edge.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            edge.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            FrameworkElementFactory presenter=new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center); edge.AppendChild(presenter); button.Template=new ControlTemplate(typeof(Button)) { VisualTree=edge };
            BindMove(button,item);
            System.Windows.Automation.AutomationProperties.SetName(button,(item.Slot+1)+" "+item.Item.Label+" · 配置を編集");
            buttons.Add(button); Put(button,p.X-size/2,p.Y-size/2);
        }
        void AddLabel(EditorTarget item,List<Rect> placed) {
            string caption=item.Item.Kind=="none" && (item.Depth>0 || appearance.EvenSpacing)?"＋ 追加":item.Item.Label;
            Button label=EditorUi.Button(caption,delegate { if(SelectionChanged!=null) SelectionChanged(item.Path,item.Slot); },false);
            label.MaxWidth=146; label.Padding=new Thickness(10,7,10,7); label.Margin=new Thickness(0); label.ToolTip=item.Item.Label;
            label.Tag=item; BindMove(label,item);
            BindEraseMenu(label,item);
            label.Content=new TextBlock { Text=caption,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=124,FontSize=13,FontWeight=FontWeights.SemiBold };
            label.Measure(new Size(146,60)); double width=label.DesiredSize.Width,height=label.DesiredSize.Height;
            Vector direction=item.Position-new Point(205,205); direction.Normalize();
            Point center=Project(item.Position)+direction*(item.Size*EditorScale/2+14+Math.Abs(direction.X)*width/2+Math.Abs(direction.Y)*height/2);
            Rect rectangle=new Rect(center.X-width/2,center.Y-height/2,width,height);
            for(int attempt=0;attempt<128 && (layout.Targets.Any(t=>TouchesCircle(rectangle,Project(t.Position),t.Size*EditorScale/2+5)) || placed.Any(r=>r.IntersectsWith(rectangle)));attempt++) rectangle.Offset(direction*8);
            placed.Add(rectangle); Put(label,rectangle.X,rectangle.Y);
        }
        static bool TouchesCircle(Rect rectangle,Point center,double radius) { double x=Math.Max(rectangle.Left,Math.Min(center.X,rectangle.Right)),y=Math.Max(rectangle.Top,Math.Min(center.Y,rectangle.Bottom)); return (new Point(x,y)-center).LengthSquared<radius*radius; }
        void BindMove(Button button,EditorTarget item) {
            button.MouseDoubleClick+=delegate(object sender,MouseButtonEventArgs e) { CancelMove(true); if(Entered!=null) Entered(item.Path,item.Slot); e.Handled=true; };
            button.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(e.ClickCount==1) BeginMove(button,item,e.GetPosition(this)); };
            button.LostMouseCapture+=delegate { if(Object.ReferenceEquals(pressed,button)) CancelMove(false); };
        }
        void BeginMove(Button button,EditorTarget item,Point point) {
            CancelMove(true); pressed=button; movingItem=item; start=point;
        }
        bool UpdateMove(Point point,bool down) {
            if(!down) { CancelMove(true); return false; }
            if(pressed==null || movingItem.Item.Kind=="none") return false;
            if(!moving && (point-start).Length<6) return false;
            moving=true; target=DropSlot(point); insertion=target<0?InsertionAt(point,movingItem.Item,movingItem):null;
            if(target>=0) { EditorTarget destination=layout.Targets[target]; EditorMove plan=EditorMove.Plan(rootItems,movingItem.Path,movingItem.Slot,destination.Path,destination.Slot); moveHint=plan.Allowed?plan.Hint:plan.Error; if(!plan.Allowed) target=-1; } else moveHint=null;
            Highlight(); return true;
        }
        bool FinishMove(Point point) {
            if(!moving && pressed!=null) UpdateMove(point,true);
            if(!moving) { CancelMove(false); return false; }
            EditorTarget from=movingItem; int index=DropSlot(point);
            EditorTarget gap=index<0?InsertionAt(point,from.Item,from):null;
            if(gap!=null) { CancelMove(true); if(Inserted!=null) Inserted(gap.Path,gap.Slot,from.Item,from.Slot); return true; }
            EditorTarget to=index<0?null:layout.Targets[index];
            CancelMove(true);
            if(to!=null && !(from.Slot==to.Slot && from.Path.SequenceEqual(to.Path)) && Moved!=null) Moved(from.Path,from.Slot,to.Path,to.Slot);
            return true;
        }
        void CancelMove(bool release) {
            Button source=pressed; pressed=null; movingItem=null; moving=false; target=-1; insertion=null; moveHint=null;
            if(release && source!=null && source.IsMouseCaptured) source.ReleaseMouseCapture();
            Highlight();
        }
        void BindEraseMenu(Button button,EditorTarget item) {
            if(item.Item.Kind=="none") return;
            ContextMenu menu=new ContextMenu();
            MenuItem erase=new MenuItem { Header="消去" };
            erase.Click+=delegate { if(Erased!=null) Erased(item.Path,item.Slot); };
            menu.Items.Add(erase); button.ContextMenu=menu;
        }
        public Point LabelCenter(int index,double width,double height) {
            Vector direction=Position(index)-Project(new Point(205,205)); direction.Normalize();
            return Position(index)+direction*(EditorButtonSize*EditorScale/2+14+Math.Abs(direction.X)*width/2+Math.Abs(direction.Y)*height/2);
        }
        void Highlight() {
            if(layout==null) return;
            for(int i=0;i<buttons.Count;i++) { bool selected=layout.IsSelected(layout.Targets[i]),expanded=layout.IsExpanded(layout.Targets[i]); buttons[i].Background=selected || i==target?appearance.Selected:Ui.Panel; buttons[i].BorderBrush=selected || expanded || i==target?appearance.Accent:Ui.Line; }
            EditorTarget chosen=layout.Targets.FirstOrDefault(layout.IsSelected);
            if(centerTitle!=null) { centerTitle.Text=target>=0?"ここに配置":chosen!=null && chosen.Item.Kind=="stack"?"スタック":"配置を編集"; centerDetail.Text=chosen!=null && chosen.Item.Kind=="stack"?chosen.Item.Label:"ドラッグで並べ替え"; }
            if(insertionMarker!=null) {
                insertionMarker.Visibility=insertion==null?Visibility.Collapsed:Visibility.Visible;
                if(insertion!=null) { Point p=Project(insertion.Position); SetLeft(insertionMarker,p.X-19); SetTop(insertionMarker,p.Y-19); centerTitle.Text="ここに挿入"; centerDetail.Text="離すと項目の間に配置"; }
            }
            if(insertion==null && moving && moveHint!=null) { centerTitle.Text=target>=0?moveHint:"移動できません"; centerDetail.Text=target>=0?layout.Targets[target].Item.Label:moveHint; }
        }
        internal EditorTarget InsertionAt(Point point,ActionItem action,EditorTarget from) {
            if(layout==null || action==null) return null;
            foreach(PlanetLayer layer in layout.Menu.Layers.Where(l=>l.Uniform).Reverse()) {
                EditorTarget[] shown=layout.Targets.Where(t=>t.Depth==layer.Depth && t.Item.Kind!="none").ToArray();
                if(shown.Length==0) continue;
                int[] path=shown[0].Path;
                if(from!=null && !from.Path.SequenceEqual(path)) continue;
                IList<ActionItem> items=PlanetEditorLayout.Resolve(rootItems,path);
                for(int i=0;i<shown.Length;i++) {
                    double angle=shown[i].Angle+Math.PI/shown.Length;
                    Point position=PlanetMenu.Polar(layer.Radius,angle);
                    if((Project(position)-point).Length>36) continue;
                    int before=i==shown.Length-1?items.Count:shown[i+1].Slot,selected;
                    if(EditorInsertion.Prepare(items,before,action,from==null?-1:from.Slot,path.Length,out selected)==null) return null;
                    return new EditorTarget { Path=path,Slot=before,Position=position };
                }
            }
            return null;
        }
        bool CanDrop(int index) {
            if(!EditorDrag.Active || EditorDrag.Item==null || index<0 || index>=layout.Targets.Count) return false;
            EditorTarget item=layout.Targets[index];
            return EditorDrag.Item.Kind!="stack" || item.Depth<PlanetStack.MaxDepth;
        }
        void Over(object sender,DragEventArgs e) {
            target=DropSlot(e.GetPosition(this)); insertion=target<0 && EditorDrag.Active?InsertionAt(e.GetPosition(this),EditorDrag.Item,null):null; if(!CanDrop(target)) target=-1;
            e.Effects=target<0 && insertion==null?DragDropEffects.None:DragDropEffects.Copy; Highlight(); e.Handled=true;
        }
        void Dropped(object sender,DragEventArgs e) {
            int to=DropSlot(e.GetPosition(this));
            EditorTarget gap=to<0 && EditorDrag.Active?InsertionAt(e.GetPosition(this),EditorDrag.Item,null):null;
            insertion=null;
            if(gap!=null) { target=-1; Highlight(); e.Handled=true; e.Effects=DragDropEffects.Copy; if(Inserted!=null) Inserted(gap.Path,gap.Slot,EditorDrag.Item,-1); return; }
            bool allowed=CanDrop(to); target=-1; Highlight(); e.Handled=true; e.Effects=DragDropEffects.None;
            if(!allowed) return; EditorTarget item=layout.Targets[to];
            if(Assigned!=null) { Assigned(item.Path,item.Slot,EditorDrag.Item.Clone()); e.Effects=DragDropEffects.Copy; }
        }
    }
}
