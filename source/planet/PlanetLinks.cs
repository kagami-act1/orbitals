using System;
using System.Linq;

namespace OrbitRing {
    static class PlanetLinks {
        public static bool Valid(string value) { Guid id; return value=="main" || value!=null && Guid.TryParseExact(value,"N",out id); }
        public static int? Index(Preferences config,string value) {
            if(value=="main") return 0;
            for(int i=0;i<config.Planets.Count;i++) if(String.Equals(config.Planets[i].Id,value,StringComparison.OrdinalIgnoreCase)) return i+1;
            return null;
        }
        public static void Options(Preferences config,string current,out string[] codes,out string[] names) {
            codes=new[]{""}.Concat(Enumerable.Range(0,config.Planets.Count+1).Select(i=>ApplicationProfiles.PlanetId(config,i))).ToArray();
            names=new[]{"開くplanetを選択"}.Concat(Enumerable.Range(0,config.Planets.Count+1).Select(i=>UserContent.Name(config,i))).ToArray();
            if(!String.IsNullOrEmpty(current) && !codes.Contains(current)) { codes=new[]{current}.Concat(codes).ToArray(); names=new[]{"削除されたplanet（選び直してください）"}.Concat(names).ToArray(); }
        }
    }
}
