using System;
using System.Windows;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    sealed partial class Controller {
        System.Drawing.Icon stoppedTrayIcon,errorTrayIcon;
        bool settingsLoadFailed,planetRegistrationFailed;
        AppIconState TrayState { get { return AppIcon.State(paused,Config.PlanetEnabled,Config.Orbit.Enabled,settingsLoadFailed || Config.PlanetEnabled && planetRegistrationFailed); } }
        Forms.ContextMenuStrip CreateTrayMenu() {
            Forms.ContextMenuStrip menu=new Forms.ContextMenuStrip();
            menu.Items.Add("設定を開く",null,delegate { OpenSettings(); });
            menu.Items.Add("変換の履歴",null,delegate { ShowConversionActivity(); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("終了",null,delegate { Application.Current.Shutdown(); });
            return menu;
        }
        void TrayMouseUp(object sender,Forms.MouseEventArgs e) { if(e.Button==Forms.MouseButtons.Left) TogglePause(); }
        void RefreshTrayState() {
            if(pauseMenu!=null) { pauseMenu.Checked=paused; pauseMenu.Text=paused?"再開":"一時停止"; }
            if(tray==null) return;
            if(trayIcon==null) trayIcon=AppIcon.Tray(AppIconState.Running,AppIcon.TraySize);
            AppIconState state=TrayState;
            if(state==AppIconState.Stopped && stoppedTrayIcon==null) stoppedTrayIcon=AppIcon.Tray(state,trayIcon.Width);
            if(state==AppIconState.Error && errorTrayIcon==null) errorTrayIcon=AppIcon.Tray(state,trayIcon.Width);
            System.Drawing.Icon icon=state==AppIconState.Stopped?stoppedTrayIcon:state==AppIconState.Error?errorTrayIcon:trayIcon;
            if(!Object.ReferenceEquals(tray.Icon,icon)) tray.Icon=icon;
            string status=paused?"一時停止":state==AppIconState.Stopped?"planet・orbitがOFF":state==AppIconState.Error?(settingsLoadFailed?"設定の読み込みエラー":"呼び出しキーの登録エラー"):"ON";
            tray.Text="Orbitals · "+status+"\nクリックで"+(paused?"再開":"一時停止")+"／右クリックで設定";
        }
    }
}
