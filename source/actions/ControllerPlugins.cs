using System;
using System.Globalization;
using System.Windows;

namespace OrbitRing {
    sealed partial class Controller {
        bool pluginsDisposed;
        async void RunPlugin(PluginAction action,IntPtr destination,string application,Action complete) {
            Native.POINT point; Native.GetCursorPos(out point);
            PluginRequest request=new PluginRequest { Version=1,ActionId=action.Id,Application=application,WindowHandle=destination.ToInt64().ToString(CultureInfo.InvariantCulture),CursorX=point.X,CursorY=point.Y };
            try {
                string message=await pluginRunner.Run(action,request);
                if(pluginsDisposed) return;
                if(complete!=null) complete(); if(message.Length>0) Notify(message);
            } catch(OperationCanceledException) { }
            catch(Exception ex) { if(!pluginsDisposed) Notify(ex.Message); }
        }
    }
}
