using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitRing {
    public sealed class UpdatePreferences {
        public string Repository { get; set; }
        public UpdatePreferences() { Repository=""; }
        public void Validate() { if(!String.IsNullOrEmpty(Repository) && UpdateService.Repository(Repository)==null) throw new Exception("更新の公開先は owner/repository またはGitHubのリポジトリURLで指定してください。"); }
    }
    sealed class ReleaseAsset {
        public string name { get; set; }
        public string state { get; set; }
        public long size { get; set; }
        public string browser_download_url { get; set; }
    }
    sealed class ReleaseMetadata {
        public string tag_name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public List<ReleaseAsset> assets { get; set; }
    }
    sealed class UpdateResult {
        public string Repository,Tag,Page;
        public Version Version;
        public bool Available { get { return Version!=null && Version>UpdateService.Current; } }
    }
    static class UpdateService {
        public static readonly Version Current=typeof(UpdateService).Assembly.GetName().Version;
        public static string Repository(string text) {
            if(text==null || text.Length>240) return null;
            string value=text.Trim(); Uri url;
            if(value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)) {
                if(!Uri.TryCreate(value,UriKind.Absolute,out url) || url.Host!="github.com" || !url.IsDefaultPort || url.UserInfo!="" || url.Query!="" || url.Fragment!="") return null;
                value=url.AbsolutePath.Trim('/');
            }
            if(value.EndsWith(".git",StringComparison.OrdinalIgnoreCase)) value=value.Substring(0,value.Length-4);
            return Regex.IsMatch(value,@"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?/[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}\z") && !value.Split('/').Any(p=>p=="." || p=="..")?value:null;
        }
        public static Version VersionTag(string tag) {
            if(tag==null || tag.Length>44 || !Regex.IsMatch(tag,@"\Av?[0-9]+\.[0-9]+(?:\.[0-9]+){0,2}\z")) return null;
            int[] parts=new int[4]; string[] tokens=tag.TrimStart('v').Split('.');
            for(int i=0;i<tokens.Length;i++) if(!Int32.TryParse(tokens[i],out parts[i])) return null;
            return new Version(parts[0],parts[1],parts[2],parts[3]);
        }
        public static UpdateResult Parse(string repository,string json) {
            string source=Repository(repository); if(source==null) throw new Exception("GitHubの公開先を指定してください。");
            if(json==null || json.Length>512*1024) throw new Exception("更新情報のサイズが不正です。");
            ReleaseMetadata release=Json.Read<ReleaseMetadata>(json);
            if(release==null || release.draft || release.prerelease) return new UpdateResult { Repository=source };
            Version version=VersionTag(release.tag_name); if(version==null) return new UpdateResult { Repository=source };
            string downloadPrefix="https://github.com/"+source+"/releases/download/"+release.tag_name+"/";
            bool packaged=release.assets!=null && release.assets.Any(a=>a!=null && a.state=="uploaded" && a.size>0 && new[]{"orbitals-Portable-Windows11.zip","orbitals-Windows11.zip","orbitals-Setup.exe"}.Contains(a.name) && String.Equals(a.browser_download_url,downloadPrefix+a.name,StringComparison.OrdinalIgnoreCase));
            return new UpdateResult { Repository=source,Tag=packaged?release.tag_name:null,Version=packaged?version:null,Page=packaged?"https://github.com/"+source+"/releases/tag/"+release.tag_name:null };
        }
        public static async Task<UpdateResult> Fetch(string repository,CancellationToken cancellation) {
            string source=Repository(repository); if(source==null) throw new Exception("GitHubの公開先を指定してください。");
            using(HttpClientHandler handler=new HttpClientHandler { AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false })
            using(HttpClient client=new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(12),MaxResponseContentBufferSize=512*1024 }) {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("orbitals/"+Current.ToString(3)); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
                using(HttpResponseMessage response=await client.GetAsync("https://api.github.com/repos/"+source+"/releases/latest",cancellation).ConfigureAwait(false)) {
                    if(response.StatusCode==HttpStatusCode.NotFound) throw new Exception("公開先または正式リリースが見つかりません。非公開リポジトリは更新確認に対応していません。");
                    if((int)response.StatusCode==403 || (int)response.StatusCode==429) throw new Exception("GitHubのアクセス制限に達しました。時間を置いて再度確認してください。");
                    if(!response.IsSuccessStatusCode) throw new Exception("GitHubから更新情報を取得できませんでした。");
                    return Parse(source,await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
            }
        }
        public static string DefaultRepository() {
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"update-channel.json");
            if(!File.Exists(path)) return "";
            using(FileStream stream=File.OpenRead(path)) {
                if(stream.Length>2048) throw new Exception("更新の公開先ファイルが大きすぎます。");
                using(StreamReader reader=new StreamReader(stream)) { UpdatePreferences channel=Json.Read<UpdatePreferences>(reader.ReadToEnd()); if(channel==null) throw new Exception("更新の公開先が空です。"); channel.Validate(); return Repository(channel.Repository)??""; }
            }
        }
    }
}
