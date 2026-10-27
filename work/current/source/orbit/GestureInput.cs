namespace OrbitRing {
    static class GestureInput {
        public static string ButtonName(string button) { return button=="Right"?"右ボタン":button=="Middle"?"中央ボタン":button=="X1"?"サイドボタン1":"サイドボタン2"; }
        public static string ChordPattern(int message,uint data) { return message==0x201?"Rocker":message==0x20A?(unchecked((short)(data>>16))>0?"WheelUp":"WheelDown"):null; }
        public static string Caption(string pattern,string button) {
            string held=ButtonName(button)+"を押したまま\n";
            return pattern=="WheelUp"?held+"ホイールを上に回す":pattern=="WheelDown"?held+"ホイールを下に回す":pattern=="Rocker"?held+"左クリック":"";
        }
    }
}
