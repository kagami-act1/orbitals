using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    sealed class MaterialIcon {
        public readonly string Name,Label,Category,Keywords;
        readonly string path;
        Geometry geometry;
        DrawingImage image;
        public string Key { get { return "ms:"+Name; } }
        public MaterialIcon(string[] fields) {
            Name=fields[0]; Label=fields[1]; Category=fields[2];
            Keywords=MaterialIcons.Normalize(fields[3]+" "+Name.Replace('_',' ')); path=fields[5];
        }
        public Geometry Shape {
            get { if(geometry==null) { geometry=Geometry.Parse("F1 "+path); geometry.Freeze(); } return geometry; }
        }
        public FrameworkElement Visual(double size) {
            if(image!=null) return new Image { Width=size,Height=size,Source=image,Stretch=Stretch.Uniform,IsHitTestVisible=false };
            DrawingGroup drawing=new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,24,24))));
            drawing.Children.Add(new GeometryDrawing(Ui.Ink,null,Shape));
            image=new DrawingImage(drawing); image.Freeze(); return new Image { Width=size,Height=size,Source=image,Stretch=Stretch.Uniform,IsHitTestVisible=false };
        }
    }
    sealed class IconCategory {
        public readonly string Id,Label;
        public int Count;
        public IconCategory(string id,string label) { Id=id; Label=label; }
        public override string ToString() { return Label+"  ("+Count+")"; }
    }
    static class MaterialIcons {
        static List<MaterialIcon> all;
        static List<IconCategory> categories;
        static readonly Dictionary<string,MaterialIcon> byKey=new Dictionary<string,MaterialIcon>(StringComparer.Ordinal);
        public static List<MaterialIcon> All { get { EnsureLoaded(); return all; } }
        public static List<IconCategory> Categories { get { EnsureLoaded(); return categories; } }
        internal static int CachedCount { get { return byKey.Count; } }
        internal static bool CatalogLoaded { get { return all!=null; } }
        static void EnsureLoaded() {
            if(all!=null) return;
            all=new List<MaterialIcon>(); categories=new List<IconCategory>(); byKey.Clear();
            Assembly assembly=Assembly.GetExecutingAssembly();
            using(Stream stream=assembly.GetManifestResourceStream("OrbitRing.IconCategories"))
            using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)) {
                string line; while((line=reader.ReadLine())!=null) { string[] fields=line.Split('\t'); if(fields.Length==2) categories.Add(new IconCategory(fields[0],fields[1])); }
            }
            using(Stream stream=assembly.GetManifestResourceStream("OrbitRing.IconCatalog"))
            using(GZipStream gzip=new GZipStream(stream,CompressionMode.Decompress))
            using(StreamReader reader=new StreamReader(gzip,Encoding.UTF8)) {
                string line; while((line=reader.ReadLine())!=null) {
                    string[] fields=line.Split('\t');
                    if(fields.Length!=6) throw new InvalidDataException("アイコン一覧が破損しています。");
                    MaterialIcon icon=new MaterialIcon(fields);
                    IconCategory category=categories.Single(c=>c.Id==icon.Category); category.Count++;
                    all.Add(icon); byKey.Add(icon.Key,icon);
                }
            }
        }
        public static string Normalize(string value) { return (value??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant(); }
        public static void Prepare(IEnumerable<string> keys) {
            if(all!=null) return;
            HashSet<string> needed=new HashSet<string>(keys.Where(key=>key!=null && key.StartsWith("ms:",StringComparison.Ordinal) && !byKey.ContainsKey(key)),StringComparer.Ordinal); if(needed.Count==0) return;
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitRing.IconCatalog")) using(GZipStream gzip=new GZipStream(stream,CompressionMode.Decompress)) using(StreamReader reader=new StreamReader(gzip,Encoding.UTF8)) {
                string line; while(needed.Count>0 && (line=reader.ReadLine())!=null) { int end=line.IndexOf('\t'); if(end<0) continue; string key="ms:"+line.Substring(0,end); if(needed.Remove(key)) byKey[key]=new MaterialIcon(line.Split('\t')); }
            }
        }
        public static MaterialIcon Find(string key) {
            if(key==null || !key.StartsWith("ms:",StringComparison.Ordinal)) return null;
            MaterialIcon icon; if(byKey.TryGetValue(key,out icon)) return icon;
            if(all!=null) return null;
            // A live ring needs only its selected icons, not 4,001 search records.
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitRing.IconCatalog"))
            using(GZipStream gzip=new GZipStream(stream,CompressionMode.Decompress))
            using(StreamReader reader=new StreamReader(gzip,Encoding.UTF8)) {
                string prefix=key.Substring(3)+"\t",line;
                while((line=reader.ReadLine())!=null) if(line.StartsWith(prefix,StringComparison.Ordinal)) {
                    icon=new MaterialIcon(line.Split('\t')); byKey.Add(key,icon); return icon;
                }
            }
            return null;
        }
        public static void ReleaseCatalog(Preferences config) {
            HashSet<string> keep=new HashSet<string>(StringComparer.Ordinal);
            foreach(ActionItem item in config.AllActions()) {
                keep.Add(ActionIcons.EffectiveKey(item));
                if(AudioMute.Supports(item)) { keep.Add(ActionIcons.EffectiveKey(AudioMute.StateAction(item,true))); keep.Add(ActionIcons.EffectiveKey(AudioMute.StateAction(item,false))); }
                if(item.Toggle!=null && item.Toggle.Stages!=null) foreach(ToggleStage stage in item.Toggle.Stages) if(stage!=null) keep.Add(ActionIcons.EffectiveKey(stage.Action()));
            }
            foreach(DropKind source in ConversionCatalog.Sources) foreach(ConversionChoice choice in config.Conversion.List(source)) if(choice.Enabled) keep.Add(ActionIcons.EffectiveKey(choice.Action()));
            // Keep only configured resources that were actually used; cleanup must not load icons.
            foreach(string key in new List<string>(byKey.Keys)) if(!keep.Contains(key)) byKey.Remove(key);
            CustomIcons.Retain(keep);
            all=null; categories=null;
        }
        public static List<MaterialIcon> Search(string query,string category) {
            string[] words=Normalize(query).Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            return All.Where(icon=>(String.IsNullOrEmpty(category) || icon.Category==category) && words.All(word=>icon.Keywords.IndexOf(word,StringComparison.Ordinal)>=0)).ToList();
        }
    }
}
