using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        ComboBox extendedValues;
        string[] extendedCodes;
        public void OpenOrbit() {
            Navigate(1);
        }
        void RefreshExtendedValues(ActionItem item) {
            string[] names; ActionOptions.Get(item.Kind,item.Value,out extendedCodes,out names,draft);
            if(item.Kind=="plugin") valueHint.Text="全体設定で読み込んだプラグインの操作を選びます。対象アプリが指定されている場合は、そのアプリで実行できます。";
            if(item.Kind=="audiodevice" && extendedCodes.Length==0) valueHint.Text="接続済みの音声出力機器がありません。接続してから選び直してください。";
            extendedValues.ItemsSource=names; extendedValues.SelectedIndex=extendedCodes==null?-1:Array.IndexOf(extendedCodes,item.Value); extendedValues.Visibility=extendedCodes==null?Visibility.Collapsed:Visibility.Visible;
            actionValue.Visibility=extendedCodes!=null || item.Kind=="pasteplain" || item.Kind=="microphone" || item.Kind=="output" || item.Kind=="volume"?Visibility.Collapsed:Visibility.Visible;
        }
    }
}
