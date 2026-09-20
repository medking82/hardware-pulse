using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HardwarePulse {
    // Local supplementary source. No HTTP, credentials, shell execution or raw-input logging.
    public static class ClaudeStatusLineReceiver {
        const int InputLimit=65536,StoreLimit=4096;
        public static int Run(string state){
            try{
                if(!Console.IsInputRedirected)return 2;
                var task=Task.Run(()=>Receive(state,new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false,true)),DateTimeOffset.UtcNow));
                if(!task.Wait(2000))return 1;
                Console.Out.Write("Pulse · "+task.Result);
                return task.Result=="CLI snapshot"||task.Result=="Quota unavailable"||task.Result=="Quota stale"?0:1;
            }catch(Exception){return 1;} // No raw input or exception details in the app's host-error log.
        }
        static JavaScriptSerializer Serializer(int limit){return new JavaScriptSerializer{MaxJsonLength=limit,RecursionLimit=16};}
        static string BoundedRead(TextReader input,int limit){
            var result=new StringBuilder();var buffer=new char[1024];int count;
            while((count=input.Read(buffer,0,Math.Min(buffer.Length,limit+1-result.Length)))>0){result.Append(buffer,0,count);if(result.Length>limit)throw new InvalidDataException();}
            return result.ToString();
        }
        static string Fingerprint(string session){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(session))).Replace("-","").ToLowerInvariant();}
        static Dictionary<string,object> ReadStore(string path){
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
            using(var reader=new StreamReader(stream,new UTF8Encoding(false,true)))return Serializer(StoreLimit).Deserialize<Dictionary<string,object>>(BoundedRead(reader,StoreLimit));
        }
        public static string Receive(string state,TextReader input,DateTimeOffset now){
            // Fixed status strings only, including malformed inputs. Never propagate parser data.
            try{
                var body=Serializer(InputLimit).Deserialize<Dictionary<string,object>>(BoundedRead(input,InputLimit));
                string session=QuotaDecoder.Text(QuotaDecoder.Get(body,"session_id"));
                if(String.IsNullOrWhiteSpace(session)||session.Length>128)return "Invalid snapshot";
                foreach(char ch in session)if(char.IsControl(ch))return "Invalid snapshot";
                string owner=Fingerprint(session),directory=Path.Combine(state,"claude-statusline");
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"snapshot.json");
                // FileShare.None serializes read/compare/replace across processes and sessions.
                using(var gate=new FileStream(Path.Combine(directory,"write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
                    ClaudeStatusLineSnapshot previous=null;
                    if(File.Exists(path)){
                        var saved=ReadStore(path);
                        if(!Equals(QuotaDecoder.Get(saved,"schema"),1))return "Invalid stored snapshot";
                        if(QuotaDecoder.Text(QuotaDecoder.Get(saved,"owner"))!=owner)return "Different CLI session";
                        previous=ClaudeStatusLineSnapshot.Restore(QuotaDecoder.Get(saved,"windows"),now);
                    }
                    var snapshot=ClaudeStatusLineSnapshot.Decode(body,now,previous);
                    var data=new Dictionary<string,object>{{"schema",1},{"owner",owner},{"windows",snapshot.Export()}};
                    string encoded=Serializer(StoreLimit).Serialize(data);
                    // At most one interrupted temporary file; the exclusive gate owns reuse.
                    string temp=Path.Combine(directory,"snapshot.tmp");
                    try{
                        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
                        using(var writer=new StreamWriter(file,new UTF8Encoding(false))){writer.Write(encoded);writer.Flush();file.Flush(true);}
                        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
                    }finally{if(File.Exists(temp))File.Delete(temp);}
                    return snapshot.Read(now).Status;
                }
            }catch(InvalidDataException){return "Invalid snapshot";}catch(IOException){return "Snapshot unavailable";}catch(UnauthorizedAccessException){return "Snapshot unavailable";}
            catch(ArgumentException){return "Invalid snapshot";}catch(InvalidOperationException){return "Invalid snapshot";}
        }
        public static QuotaReading Read(string state,DateTimeOffset now){
            try{
                var stored=ReadStore(Path.Combine(state,"claude-statusline","snapshot.json"));
                if(Equals(QuotaDecoder.Get(stored,"schema"),1))return ClaudeStatusLineSnapshot.Restore(QuotaDecoder.Get(stored,"windows"),now).Read(now);
            }catch(InvalidDataException){}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
            return new QuotaReading{Provider="Claude",Source="CLI snapshot",Status="Quota unavailable",Observed=now};
        }
    }
}
