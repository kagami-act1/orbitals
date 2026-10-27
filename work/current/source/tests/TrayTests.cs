using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    static class TrayTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static object Call(object target,string name,params object[] args) { return target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static bool Paused(Controller owner) { return (bool)typeof(Controller).GetField("paused",Private).GetValue(owner); }
        static void Click(Controller owner,Forms.MouseButtons button) { Call(owner,"TrayMouseUp",null,new Forms.MouseEventArgs(button,1,0,0,0)); }
        public static List<string> Run() {
            List<string> r=new List<string>();
            using(Controller owner=new Controller(true)) using(Forms.ContextMenuStrip menu=(Forms.ContextMenuStrip)Call(owner,"CreateTrayMenu")) {
                Call(owner,"InitializeOrbit",menu); Call(owner,"InitializeFeatureMenus",menu);
                string[] names=menu.Items.Cast<Forms.ToolStripItem>().Select(item=>item.Text).ToArray();
                Check(!names.Contains("ファイル変換") && !names.Contains("planetを試す") && !names.Contains("orbitを設定") && !names.Contains("更新を確認") && names.Contains("設定を開く") && names.Contains("一時停止"),"the tray menu keeps settings and pause without duplicate navigation entries",r);
                Forms.ToolStripMenuItem pause=menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item=>item.Text=="一時停止");
                string original=Json.Write(owner.Config);
                Click(owner,Forms.MouseButtons.Right); Click(owner,Forms.MouseButtons.Middle);
                Check(!Paused(owner) && owner.OrbitAllowed,"right and middle tray clicks do not toggle input",r);
                Click(owner,Forms.MouseButtons.Left);
                Check(Paused(owner) && !owner.OrbitAllowed && pause.Checked && pause.Text=="再開","one left click pauses input and updates the tray menu state",r);
                Preferences config=owner.Config.Clone(); config.Orbit.Enabled=false; owner.Apply(config); Call(owner,"RefreshTrayState");
                Check(Paused(owner) && pause.Text=="再開","saving individual feature settings cannot cancel the temporary pause",r);
                Click(owner,Forms.MouseButtons.Left);
                Check(!Paused(owner) && !owner.OrbitAllowed && !pause.Checked && pause.Text=="一時停止" && !owner.Config.Orbit.Enabled,"resuming preserves the independently disabled orbit setting",r);
                config.Orbit.Enabled=true; owner.Apply(config);
                Check(owner.OrbitAllowed && Json.Write(owner.Config)==original,"pause and resume leave saved actions and enabled settings unchanged",r);
                Click(owner,Forms.MouseButtons.Left); Click(owner,Forms.MouseButtons.Left);
                Check(!Paused(owner),"each left-button release toggles once, including rapid pairs of clicks",r);
                using(System.Drawing.Icon normal=AppIcon.Tray()) using(System.Drawing.Icon paused=AppIcon.Tray(AppIconState.Stopped)) using(System.Drawing.Bitmap a=normal.ToBitmap()) using(System.Drawing.Bitmap b=paused.ToBitmap()) {
                    int changed=0; for(int y=0;y<32;y++) for(int x=0;x<32;x++) if(a.GetPixel(x,y)!=b.GetPixel(x,y)) changed++;
                    Check(paused.Width==32 && paused.Height==32 && changed>100,"the stopped tray state uses the supplied distinct artwork",r);
                }
            }
            return r;
        }
    }
}
