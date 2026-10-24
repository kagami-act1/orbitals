using System;
using System.Windows;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    sealed partial class Controller {
        System.Drawing.Icon pausedTrayIcon;
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
            if(paused && pausedTrayIcon==null) pausedTrayIcon=AppIcon.PausedTray();
            tray.Icon=paused?pausedTrayIcon:trayIcon;
            tray.Text="Orbitals · "+(paused?"一時停止":"ON")+"\nクリックで"+(paused?"再開":"一時停止")+"／右クリックで設定";
        }
    }
}
