using System;
using System.IO;
using System.Reflection;
using System.Text;
using HardwarePulse;

static class QuotaLoginFileTests {
    static readonly MethodInfo Reader=typeof(QuotaProviders).GetMethod("ReadLogin",BindingFlags.NonPublic|BindingFlags.Static);
    static void Check(bool ok,string message){if(!ok)throw new Exception("Login file: "+message);}
    static object Read(string path){try{return Reader.Invoke(null,new object[]{path});}catch(TargetInvocationException error){throw error.InnerException;}}
    public static void Run(){
        string root=Path.Combine(Environment.CurrentDirectory,"vendor","quota-login-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string path=Path.Combine(root,"synthetic.json");
        File.WriteAllText(path,"{\"fixture\":\"first\"}",new UTF8Encoding(false));
        using(var writer=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite|FileShare.Delete)){
            Check(QuotaDecoder.Text(QuotaDecoder.Get(Read(path),"fixture"))=="first","compatible writer handle prevents reading");
            string replacement=Path.Combine(root,"replacement.json");File.WriteAllText(replacement,"{\"fixture\":\"second\"}");File.Replace(replacement,path,null);
            Check(QuotaDecoder.Text(QuotaDecoder.Get(Read(path),"fixture"))=="second","next read must follow atomic replacement, not retain old data");
        }
        foreach(Encoding encoding in new Encoding[]{new UTF8Encoding(true),Encoding.Unicode,Encoding.BigEndianUnicode}){
            File.WriteAllText(path,"{\"fixture\":\"encoding\"}",encoding);
            Check(QuotaDecoder.Text(QuotaDecoder.Get(Read(path),"fixture"))=="encoding","existing BOM decoding changed");
        }
        File.WriteAllText(path,new string(' ',1048577));
        ExpectFailure(path,"Login unavailable");File.Delete(path);ExpectFailure(path,"Login required");
        File.WriteAllText(path,"{\"fixture\":");bool rejected=false;
        try{Read(path);}catch{rejected=true;}Check(rejected,"partial JSON must not be accepted");
        File.Delete(path);Directory.Delete(root);
        SelectedSource();
        Console.WriteLine("PASS login file: compatible writer, atomic replacement, BOM, size bound, missing and partial data; synthetic files only");
    }
    static void SelectedSource(){
        string root=Path.Combine(Environment.CurrentDirectory,"vendor","quota-source-"+Guid.NewGuid().ToString("N"));
        string first=Path.Combine(root,"first"),other=Path.Combine(root,"other");Directory.CreateDirectory(first);Directory.CreateDirectory(other);
        string firstPath=Path.Combine(first,".credentials.json"),otherPath=Path.Combine(other,".credentials.json");
        File.WriteAllText(firstPath,"{\"fixture\":\"selected\"}");File.WriteAllText(otherPath,"{\"fixture\":\"other\"}");
        string previous=Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        try{
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR",first);
            var factory=typeof(QuotaProviders).GetMethod("ClaudeLoginReader",BindingFlags.NonPublic|BindingFlags.Static);
            var read=(Func<object>)factory.Invoke(null,null);
            Check(QuotaDecoder.Text(QuotaDecoder.Get(read(),"fixture"))=="selected","initial configured file");
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR",other);
            Check(QuotaDecoder.Text(QuotaDecoder.Get(read(),"fixture"))=="selected","recovery changed its selected source");
            File.Delete(firstPath);bool missing=false;
            try{read();}catch(QuotaFailure failure){missing=failure.Status=="Login required";}
            Check(missing,"missing selected file must not select another file/store");
        }finally{Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR",previous);if(File.Exists(firstPath))File.Delete(firstPath);File.Delete(otherPath);Directory.Delete(first);Directory.Delete(other);Directory.Delete(root);}
        Console.WriteLine("PASS Claude login source: retry pins configured file and rejects disappearance without fallback; synthetic only");
    }
    static void ExpectFailure(string path,string status){try{Read(path);throw new Exception("Expected failure");}catch(QuotaFailure error){Check(error.Status==status,"unexpected safe status");}}
}
