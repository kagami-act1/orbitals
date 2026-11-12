using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        GestureDrawing appearanceTrail;
        StackSpacingPreview stackSpacingPreview;
        void AppearanceChanged() { UpdateAppearancePreview(); RefreshSurface(); Changed("外観を変更しました。「保存」で反映します。"); }
        void PreviewRingReveal() { if(stackSpacingPreview!=null) stackSpacingPreview.PlayReveal(); }
        void UpdateAppearancePreview() {
            if(appearanceTrail!=null) appearanceTrail.SetAppearance(draft.Appearance);
            if(stackSpacingPreview!=null) stackSpacingPreview.Update(draft.Appearance);
        }
        void AddColor(StackPanel parent,string label,string value,Action<string> set) {
            StackPanel section=Section(label); section.Margin=new Thickness(0,0,0,14); parent.Children.Add(section);
            WrapPanel row=new WrapPanel(); section.Children.Add(row);
            TextBox hex=EditorUi.Input(value); hex.Width=100; hex.MaxLength=7; hex.Margin=new Thickness(0,0,12,4); System.Windows.Automation.AutomationProperties.SetName(hex,label+" #RRGGBB"); row.Children.Add(hex);
            string accepted=value;
            hex.TextChanged+=delegate { if(!AppearancePreferences.ValidColor(hex.Text) || String.Equals(hex.Text,accepted,StringComparison.OrdinalIgnoreCase)) return; Remember(); accepted=hex.Text.ToUpperInvariant(); set(accepted); AppearanceChanged(); };
            hex.LostKeyboardFocus+=delegate { if(!AppearancePreferences.ValidColor(hex.Text)) { hex.Text=accepted; Changed("色は #RRGGBB で入力してください。"); } };
            foreach(string color in new[]{"#93E9C1","#8DBEFF","#BEA4FF","#FFA8C2","#FFD18A","#EDF3F6"}) {
                string choice=color; Button swatch=EditorUi.Button("",delegate { hex.Text=choice; },false); swatch.Width=26; swatch.Height=30; swatch.Padding=new Thickness(0); swatch.Margin=new Thickness(0,0,6,4); swatch.Background=EditorUi.Brush(choice); swatch.ToolTip=choice; System.Windows.Automation.AutomationProperties.SetName(swatch,label+" "+choice); row.Children.Add(swatch);
            }
        }
        void AddSlider(StackPanel parent,string label,double min,double max,double value,double tick,Action<double> set,Func<double,string> format) {
            DockPanel heading=new DockPanel(); parent.Children.Add(heading); TextBlock number=EditorUi.Text(format(value),12,EditorUi.Mint); DockPanel.SetDock(number,Dock.Right); heading.Children.Add(number); heading.Children.Add(EditorUi.Text(label,12,EditorUi.Muted));
            Slider slider=new Slider { Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,Margin=new Thickness(0,8,0,14) }; System.Windows.Automation.AutomationProperties.SetName(slider,label); parent.Children.Add(slider);
            slider.PreviewMouseLeftButtonDown+=delegate { sliderGesture=true; sliderHistory=false; }; slider.PreviewMouseLeftButtonUp+=delegate { sliderGesture=false; sliderHistory=false; };
            slider.LostMouseCapture+=delegate { sliderGesture=false; sliderHistory=false; };
            slider.ValueChanged+=delegate { ResizeCheckpoint(); set(slider.Value); number.Text=format(slider.Value); AppearanceChanged(); };
        }
        void ExportBackup() {
            try {
                draft.Validate(); Microsoft.Win32.SaveFileDialog dialog=new Microsoft.Win32.SaveFileDialog { Title="設定のバックアップ",InitialDirectory=AppDomain.CurrentDomain.BaseDirectory,Filter="JSONファイル (*.json)|*.json",DefaultExt=".json",AddExtension=true,FileName="orbitals-settings-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json" };
                if(dialog.ShowDialog(this)!=true) return; SettingsBackup.Write(dialog.FileName,draft); Changed("JSONファイルに書き出しました。");
            } catch(Exception ex) { OrbitDialog.Info(this,"書き出せませんでした",ex.Message); }
        }
        void ImportBackup() {
            try {
                Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog { Title="設定のJSONを読み込む",InitialDirectory=AppDomain.CurrentDomain.BaseDirectory,Filter="JSONファイル (*.json)|*.json",CheckFileExists=true };
                if(dialog.ShowDialog(this)!=true) return; Preferences restored=SettingsBackup.Read(dialog.FileName); Remember(); draft=restored; editorApplication=null; editorPlanet=0; selected=0; gestureIndex=0; stackPath.Clear(); RefreshAll(); Changed("JSONを読み込みました。内容を確認して「保存」で反映します。");
            } catch(Exception ex) { OrbitDialog.Info(this,"読み込めませんでした",ex.Message); }
        }
    }
}
