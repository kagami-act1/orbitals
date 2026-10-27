using System;
using System.IO;
using System.Linq;

namespace OrbitRing {
    static class OpenTarget {
        public static string Resolve(string value) {
            if(String.IsNullOrWhiteSpace(value) || value.Length>8192 || value.Any(Char.IsControl))
                throw new Exception("開くファイル・フォルダーの絶対パス、または http(s) URL を指定してください。");
            Uri uri;
            // URLs are kept intact; expanding %NAME% here can change their destination.
            if(Uri.TryCreate(value,UriKind.Absolute,out uri) && (uri.Scheme==Uri.UriSchemeHttps || uri.Scheme==Uri.UriSchemeHttp) && !String.IsNullOrEmpty(uri.Host))
                return value;
            string path=Environment.ExpandEnvironmentVariables(value).Replace('/','\\');
            bool drive=path.Length>=3 && ((path[0]>='A' && path[0]<='Z') || (path[0]>='a' && path[0]<='z')) && path[1]==':' && path[2]=='\\';
            bool unc=path.StartsWith(@"\\",StringComparison.Ordinal) && !path.StartsWith(@"\\?\",StringComparison.Ordinal) && !path.StartsWith(@"\\.\",StringComparison.Ordinal);
            if((!drive && !unc) || path.Skip(drive?2:0).Contains(':') || path.Any(Char.IsControl))
                throw new Exception("相対パスや特殊なURLは使えません。絶対パス、または http(s) URL を指定してください。");
            path=Path.GetFullPath(path);
            if(!File.Exists(path) && !Directory.Exists(path)) throw new Exception("開くファイルまたはフォルダーが見つかりません。");
            return path;
        }
    }
}
