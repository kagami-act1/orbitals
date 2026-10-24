using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    sealed class StackSpacingPreview : StackPanel {
        readonly List<ActionItem> root=Preferences.Defaults().Items;
        readonly Canvas scene=new Canvas();
        readonly RingSurface surface=new RingSurface();
        double buttonSize;
        AppearancePreferences appearance;
        double lastGap=Double.NaN,lastOpacity;
        string lastAccent;
        bool lastTextOnly;
        public StackSpacingPreview(AppearancePreferences value,double size=76) {
            Margin=new Thickness(0,8,0,14); appearance=value; buttonSize=size;
            root[2]=new ActionItem("スタック","stack","") { StackMode="arc",Children=new List<ActionItem> { new ActionItem("コピー","shortcut","Ctrl+C"),new ActionItem("貼り付け","shortcut","Ctrl+V"),new ActionItem("音量","volume","") } };
            ConfigureBounds(); scene.Children.Add(surface);
            Viewbox view=new Viewbox { Child=scene,Stretch=Stretch.Uniform,IsHitTestVisible=false,Margin=new Thickness(10),Height=180 };
            System.Windows.Automation.AutomationProperties.SetName(view,"リングと弧状スタックのプレビュー");
            Children.Add(new Border { Background=EditorUi.Brush("#0C1117"),BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=view }); Render();
        }
        PlanetMenu Menu(double gap) { PlanetMenu menu=new PlanetMenu(root,buttonSize,gap); menu.Open(2); return menu; }
        void ConfigureBounds() { PlanetMenu maximum=Menu(PlanetStack.MaxGap); Rect bounds=new Rect(-10,-10,430,430); foreach(PlanetTarget target in maximum.Targets) { double half=target.Size/2+22; bounds.Union(new Rect(target.Position.X-half,target.Position.Y-half,half*2,half*2)); } scene.Width=bounds.Width; scene.Height=bounds.Height; Canvas.SetLeft(surface,-bounds.Left); Canvas.SetTop(surface,-bounds.Top); }
        void Render() {
            foreach(PlanetReveal reveal in scene.Children.OfType<PlanetReveal>().ToArray()) reveal.Cancel();
            PlanetMenu menu=Menu(appearance.StackGap); Preferences view=new Preferences { Items=menu.Targets.Select(item=>item.Item).ToList(),Appearance=appearance };
            surface.Bind(view,menu); surface.UpdatePointer(surface.IconPosition(2)); surface.ShowPreviewSelection(2);
            lastGap=appearance.StackGap; lastOpacity=appearance.RingOpacity; lastAccent=appearance.RingAccent; lastTextOnly=appearance.TextOnly;
        }
        public void Update(AppearancePreferences value,double? size=null) { appearance=value; bool resized=size.HasValue && size.Value!=buttonSize; if(resized) { buttonSize=size.Value; ConfigureBounds(); } if(resized || lastGap!=value.StackGap || lastOpacity!=value.RingOpacity || lastAccent!=value.RingAccent || lastTextOnly!=value.TextOnly) Render(); }
        public void PlayReveal() { PlanetReveal.Play(scene,new Point(Canvas.GetLeft(surface),Canvas.GetTop(surface)),1,appearance.Accent,surface); }
    }
}
