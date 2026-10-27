using System;
using System.Windows.Forms;

namespace OrbitRing {
    sealed partial class Controller {
        public bool ShortcutRecording { get; private set; }
        ToolStripMenuItem planetEnabledMenu,orbitEnabledMenu;
        public void SetShortcutRecording(bool value) {
            if(ShortcutRecording==value) return;
            ShortcutRecording=value;
            if(Diagnostic) return;
            if(value) { HideRing(); ClearPlanetKeys(); CloseOrbitFrame(); }
            else if(!pluginsDisposed && !RegisterPlanetKeys(Config)) Notify("呼び出しキーを再登録できませんでした。ほかのアプリとの競合を確認してください。");
        }
        void InitializeFeatureMenus(ContextMenuStrip menu) {
            planetEnabledMenu=new ToolStripMenuItem("planet（リング）"); orbitEnabledMenu=new ToolStripMenuItem("orbit（ジェスチャー）");
            planetEnabledMenu.Click+=delegate { SetFeature(true,!Config.PlanetEnabled); };
            orbitEnabledMenu.Click+=delegate { SetFeature(false,!Config.Orbit.Enabled); };
            menu.Items.Insert(0,orbitEnabledMenu); menu.Items.Insert(0,planetEnabledMenu); RefreshFeatureMenus();
        }
        void SetFeature(bool planet,bool enabled) {
            try { Preferences next=Config.Clone(); if(planet) next.PlanetEnabled=enabled; else next.Orbit.Enabled=enabled; Apply(next); Notify((planet?"planet":"orbit")+"を"+(enabled?"ON":"OFF")+"にしました。"); }
            catch(Exception ex) { Notify(ex.Message); }
        }
        void RefreshFeatureMenus() { if(planetEnabledMenu!=null) { planetEnabledMenu.Checked=Config.PlanetEnabled; orbitEnabledMenu.Checked=Config.Orbit.Enabled; } }
        void UpdateOrbitEngine() {
            if(Diagnostic) return;
            if(Config.Orbit.Enabled && !paused) { if(orbit==null) orbit=new OrbitEngine(this); else orbit.Update(Config); }
            else { if(orbit!=null) { orbit.Dispose(); orbit=null; } CloseOrbitFrame(); }
        }
    }
}
