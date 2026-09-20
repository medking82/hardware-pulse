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
        Console.WriteLine("PASS login file: compatible writer, atomic replacement, BOM, size bound, missing and partial data; synthetic files only");
    }
    static void ExpectFailure(string path,string status){try{Read(path);throw new Exception("Expected failure");}catch(QuotaFailure error){Check(error.Status==status,"unexpected safe status");}}
}
