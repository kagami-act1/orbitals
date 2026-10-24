using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

class OrbitalsExample {
    static int Main(string[] args) {
        Console.InputEncoding=new UTF8Encoding(false);
        Console.OutputEncoding=new UTF8Encoding(false);
        try {
            string line=Console.ReadLine();
            if(line==null || line.Length>4096) throw new Exception("入力データを確認してください。");
            var request=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);
            if(Convert.ToInt32(request["Version"])!=1) throw new Exception("未対応の入力形式です。");
            // Call the target application's API here, using ActionId to select an operation.
            Console.WriteLine("連携のサンプルを実行しました："+request["ActionId"]);
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
