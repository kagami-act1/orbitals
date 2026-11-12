using System;
using System.IO;

namespace OrbitRing {
    static class ModernImageCodec {
        public static bool Supported(string path) { string extension=Path.GetExtension(path??""); return extension.Equals(".heic",StringComparison.OrdinalIgnoreCase) || extension.Equals(".heif",StringComparison.OrdinalIgnoreCase) || extension.Equals(".avif",StringComparison.OrdinalIgnoreCase); }
        public static void DecodeFallback(string source,string target,int edge,string directory) {
            string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"conversion","image-codec.exe");
            if(!File.Exists(executable)) throw new Exception("画像変換の部品が見つかりません。Orbitalsを再インストールしてください。");
            ConversionProcesses.Run(executable,new[]{source,target,edge.ToString(System.Globalization.CultureInfo.InvariantCulture)},directory,"HEIC・AVIF画像を読み込めませんでした。");
            if(!File.Exists(target) || new FileInfo(target).Length==0) throw new Exception("画像の読み込み結果が空です。");
        }
    }
}
