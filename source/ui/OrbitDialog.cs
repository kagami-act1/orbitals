using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbitRing {
    enum SaveChoice { Cancel, Discard, KeepDraft, Save }
    sealed class OrbitDialog : Window {
        SaveChoice choice=SaveChoice.Cancel;
        readonly StackPanel buttons;
        OrbitDialog(Window owner,string heading,string message) {
            Title="orbitals — "+heading; Icon=AppIcon.Image;
            Width=510; SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize;
            WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
            ShowInTaskbar=owner==null; WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
            if(owner!=null) Owner=owner;
            Foreground=EditorUi.Ink; FontFamily=new FontFamily("Segoe UI, Yu Gothic UI"); EditorUi.Apply(this);
            StackPanel panel=new StackPanel { Margin=new Thickness(26) };
            Border frame=new Border { Background=EditorUi.Bg,BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=panel }; Content=frame;
            DockPanel top=new DockPanel { Margin=new Thickness(0,0,0,22) }; panel.Children.Add(top);
            Image logo=new Image { Source=AppIcon.Image,Width=24,Height=24,Margin=new Thickness(0,0,10,0) }; top.Children.Add(logo);
            TextBlock brand=EditorUi.Text("O R B I T A L S",11,EditorUi.Mint); brand.VerticalAlignment=VerticalAlignment.Center; top.Children.Add(brand);
            top.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(e.ButtonState==MouseButtonState.Pressed) DragMove(); };
            TextBlock title=EditorUi.Text(heading,23,EditorUi.Ink); title.FontWeight=FontWeights.SemiBold; panel.Children.Add(title);
            TextBlock body=EditorUi.Text(message,14,EditorUi.Muted); body.Margin=new Thickness(0,12,0,24); body.TextWrapping=TextWrapping.Wrap; panel.Children.Add(new ScrollViewer { Content=body,MaxHeight=340,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right }; panel.Children.Add(buttons);
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { Close(); e.Handled=true; } };
        }
        void Add(string label,SaveChoice result,bool primary,bool cancel) {
            Button button=EditorUi.Button(label,delegate { choice=result; Close(); },primary); button.Margin=new Thickness(8,0,0,0); button.IsCancel=cancel; button.IsDefault=primary; buttons.Children.Add(button);
        }
        public static SaveChoice ConfirmSave(Window owner) {
            OrbitDialog dialog=new OrbitDialog(owner,"変更を保存しますか？","保存すると、次の呼び出しから変更が反映されます。");
            dialog.Width=660;
            dialog.Add("編集を続ける",SaveChoice.Cancel,false,true); dialog.Add("破棄して閉じる",SaveChoice.Discard,false,false); dialog.Add("下書きで閉じる",SaveChoice.KeepDraft,false,false); dialog.Add("保存して閉じる",SaveChoice.Save,true,false);
            dialog.ShowDialog(); return dialog.choice;
        }
        public static void Info(Window owner,string heading,string message) {
            OrbitDialog dialog=new OrbitDialog(owner,heading,message); dialog.Add("閉じる",SaveChoice.Cancel,true,true); dialog.ShowDialog();
        }
    }
}
