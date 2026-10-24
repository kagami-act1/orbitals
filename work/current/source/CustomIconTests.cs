using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class CustomIconTests {
        static void Check(bool condition,string name,List<string> results) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static BitmapSource Artwork(int width,int height,int seed,bool transparent=false) {
            byte[] pixels=new byte[width*height*4]; new Random(seed).NextBytes(pixels);
            for(int i=0;i<width*height;i++) pixels[i*4+3]=(byte)(transparent && i%width<width/2?0:255);
            return BitmapSource.Create(width,height,120,120,PixelFormats.Bgra32,null,pixels,width*4);
        }
        static void Save(string path,BitmapEncoder encoder,BitmapSource image) { encoder.Frames.Add(BitmapFrame.Create(image)); using(FileStream output=File.Create(path)) encoder.Save(output); }
        public static List<string> Run() {
            List<string> r=new List<string>(); string folder=Path.Combine(Path.GetTempPath(),"orbitals-icons-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            List<string> files=new List<string>(); string png=Path.Combine(folder,"custom image.png"); files.Add(png);
            try {
                Save(png,new PngBitmapEncoder(),Artwork(512,256,1,true));
                string key=CustomIcons.Import(png); BitmapSource image=CustomIcons.Read(key);
                Check(image.PixelWidth==128 && image.PixelHeight==64 && image.IsFrozen && image.DpiX==96,"custom image preserves aspect ratio and uses a bounded immutable bitmap",r);
                byte[] pixels=new byte[image.PixelWidth*image.PixelHeight*4]; image.CopyPixels(pixels,image.PixelWidth*4,0);
                Check(pixels[3]==0 && pixels[(image.PixelWidth-1)*4+3]==255,"PNG icon import preserves transparent and opaque pixels",r);
                File.Delete(png); CustomIcons.ClearCache();
                Check(CustomIcons.Read(key).PixelWidth==128 && !key.Contains(folder),"custom icon remains available after its original file is removed",r);
                Preferences config=Preferences.Defaults(); config.Items[0].Icon=key; config.Validate(); Preferences cloned=config.Clone(); cloned.Swap(0,3); cloned.Validate();
                Check(cloned.Items[3].Icon==key && Object.ReferenceEquals(cloned.Items[3].Icon,key),"custom icon survives JSON and reordering while sharing cached image data",r);
                Check(ActionIcons.Visual(cloned.Items[3],28) is Image && ((System.Windows.Controls.Image)ActionIcons.Visual(cloned.Items[3],28)).Source==CustomIcons.Read(key),"action rendering uses the imported bitmap",r);
                Check(((StackPanel)RingSurface.Bubble(cloned.Items[3],3,76).Content).Children[0] is Image,"runtime ring buttons display imported icons",r);
                ActionItem mute=ActionCatalog.Create().Single(p=>p.Item.Label=="ミュート切替").Create(); mute.Toggle.Stages[0].Icon=key; config.Items[1]=mute; config.Favorites.Add(UserContent.CopyAction(mute)); config.Validate();
                ToggleState state=new ToggleState(); state.SynchronizeMute(mute,true);
                Check(state.Display(mute).Icon==key && ActionIcons.Visual(state.Display(mute),28) is Image,"toggle state display preserves the selected custom icon",r);
                string backup=Path.Combine(folder,"backup.json"); files.Add(backup); SettingsBackup.Write(backup,config); Preferences restored=SettingsBackup.Read(backup);
                Check(restored.Items[0].Icon==key && restored.Favorites[0].Toggle.Stages[0].Icon==key,"JSON backups include images in actions and favorite toggle states",r);
                string draft=Path.Combine(folder,"draft.json"); files.Add(draft); WorkingCopy.Write(draft,new WorkingCopyRecord { Version=1,Config=config,StackPath=new int[0] });
                Check(WorkingCopy.Read(draft).Config.Items[1].Toggle.Stages[0].Icon==key,"draft recovery includes custom state icons",r);
                BitmapSource small=Artwork(16,8,2); BitmapEncoder[] encoders={new JpegBitmapEncoder(),new BmpBitmapEncoder(),new GifBitmapEncoder()}; string[] extensions={"jpg","bmp","gif"};
                bool formats=true;
                for(int i=0;i<encoders.Length;i++) { string path=Path.Combine(folder,"sample."+extensions[i]); files.Add(path); Save(path,encoders[i],small); formats &= CustomIcons.Read(CustomIcons.Import(path)).PixelWidth==16; }
                string ico=Path.Combine(folder,"sample.ico"); files.Add(ico); using(System.Drawing.Icon icon=AppIcon.Tray()) using(FileStream output=File.Create(ico)) icon.Save(output);
                formats &= CustomIcons.Read(CustomIcons.Import(ico)).PixelWidth>0;
                Check(formats,"JPEG BMP GIF and ICO imports become self-contained PNG icons",r);
                string invalid=Path.Combine(folder,"invalid.png"); files.Add(invalid); File.WriteAllText(invalid,"this is not an image");
                Check(Reject(delegate { CustomIcons.Import(invalid); }) && Reject(delegate { CustomIcons.Read("image:png;base64,invalid"); }) && Reject(delegate { ActionIcons.Validate("image:http://example.com/icon.png"); }),"invalid image files encoded data and remote image references are rejected",r);
                using(FileStream oversized=File.Create(invalid)) oversized.SetLength(CustomIcons.MaxFileBytes+1);
                Check(Reject(delegate { CustomIcons.Import(invalid); }),"oversized source image is rejected before reading its content",r);
                Save(png,new PngBitmapEncoder(),Artwork(129,1,3)); byte[] source=File.ReadAllBytes(png);
                Check(Reject(delegate { CustomIcons.Read("image:png;base64,"+Convert.ToBase64String(source)); }),"untrusted embedded image dimensions are checked before decoding",r);
                Save(png,new PngBitmapEncoder(),Artwork(4097,1,3));
                Check(Reject(delegate { CustomIcons.Import(png); }),"oversized source dimensions are rejected",r);
                Save(png,new PngBitmapEncoder(),Artwork(128,128,4)); string detailed=CustomIcons.Import(png);
                Preferences detailedConfig=Preferences.Defaults(); foreach(ActionItem action in detailedConfig.Items) action.Icon=detailed; string detailedJson=Json.Write(detailedConfig);
                Check(detailedJson.Length>256*1024 && Json.Read<Preferences>(detailedJson).Items.All(a=>a.Icon==detailed),"multiple detailed icons fit within the bounded JSON settings format",r);
                for(int i=0;i<40;i++) { Save(png,new PngBitmapEncoder(),Artwork(8,8,i)); CustomIcons.Import(png); }
                Check(CustomIcons.CachedCount<=32,"decoded custom icon cache has a fixed memory bound",r);
                MaterialIcons.ReleaseCatalog(config); Check(CustomIcons.CachedCount==0,"idle resource release clears decoded custom images",r);
                Check(ActionIcons.Visual(config.Items[0],28) is Image && ActionIcons.Label(key)=="自作の画像","custom images reload independently of the bundled icon catalog",r);
            } finally { foreach(string path in files) if(File.Exists(path)) File.Delete(path); Directory.Delete(folder); CustomIcons.ClearCache(); }
            return r;
        }
    }
}
