using System;
using System.IO;
using System.Text;
using HardwarePulse;

// Exercises the real DPAPI store through a fixture provider name, so no user credential is
// touched and no key material is ever printed.
internal static class QuotaCredentialTests {
    const string Fixture="QuotaCredentialFixture";
    const string Sample="fixture-key-not-a-real-key";
    static void Check(bool value,string message){if(!value)throw new Exception("Quota credential: "+message);}
    public static void Run(){
        JevSessionCrypto();
        string path=ProtectedQuotaCredentials.KeyFile(Fixture);
        try{
            if(File.Exists(path))ProtectedQuotaCredentials.Remove(Fixture);
            Check(!ProtectedQuotaCredentials.Exists(Fixture),"fixture credential must start absent");
            // Only printable ASCII of a bounded length is a key; a pasted typo is not a credential.
            foreach(string invalid in new[]{null,"","   ","\t"+Sample,Sample+" ",Sample+"\n",new string('x',16385)})Check(!ProtectedQuotaCredentials.Valid(invalid),"invalid key accepted");
            Check(ProtectedQuotaCredentials.Valid(Sample),"fixture key shape rejected");
            try{ProtectedQuotaCredentials.Save(Fixture,"bad key");throw new Exception("Invalid key stored");}
            catch(QuotaFailure failure){Check(failure.Status=="Credential unavailable","invalid save must fail closed");}
            Check(!File.Exists(path),"invalid save left a file");
            try{ProtectedQuotaCredentials.Read(Fixture);throw new Exception("Missing credential returned");}
            catch(QuotaFailure failure){Check(failure.Status=="API key required","missing key must report the actionable status");}
            ProtectedQuotaCredentials.Save(Fixture,Sample);
            Check(ProtectedQuotaCredentials.Exists(Fixture),"saved credential missing");
            Check(ProtectedQuotaCredentials.Read(Fixture)==Sample,"round trip changed the key");
            byte[] stored=File.ReadAllBytes(path);
            Check(stored.Length>0&&stored.Length<=65536,"stored blob size invalid");
            Check(!Encoding.UTF8.GetString(stored).Contains(Sample)&&!Encoding.Unicode.GetString(stored).Contains(Sample),"key text found in the protected file");
            // Per-provider entropy: another provider must not be able to read this blob.
            string other=ProtectedQuotaCredentials.KeyFile(Fixture+"Other");
            try{
                File.Copy(path,other,true);
                try{ProtectedQuotaCredentials.Read(Fixture+"Other");throw new Exception("Blob decrypted under a different provider");}
                catch(QuotaFailure failure){Check(failure.Status=="Credential unavailable","cross-provider blob must fail closed");}
            }finally{if(File.Exists(other))File.Delete(other);}
            // A truncated, foreign or oversized file is not a credential.
            foreach(byte[] damaged in new[]{new byte[0],Encoding.UTF8.GetBytes("not-a-dpapi-blob"),new byte[65537]}){
                File.WriteAllBytes(path,damaged);
                try{ProtectedQuotaCredentials.Read(Fixture);throw new Exception("Damaged credential accepted");}
                catch(QuotaFailure failure){Check(failure.Status=="Credential unavailable","damaged credential must fail closed");}
            }
            ProtectedQuotaCredentials.Save(Fixture,Sample);
            ProtectedQuotaCredentials.Remove(Fixture);
            Check(!ProtectedQuotaCredentials.Exists(Fixture),"removed credential still present");
            Console.WriteLine("PASS quota credentials: DPAPI round trip, per-provider entropy, fail-closed validation");
        }finally{
            try{if(File.Exists(path))File.Delete(path);}catch{}
            try{if(File.Exists(path+".tmp"))File.Delete(path+".tmp");}catch{}
        }
    }
    static void JevSessionCrypto(){
        var session=typeof(QuotaReading).Assembly.GetType("HardwarePulse.JevSession");Check(session!=null,"Jev needs a bounded session credential contract");
        var encode=session.GetMethod("Encode");var decode=session.GetMethod("Decode");
        string cookie="session=fixture-cookie; organization_id=fixture-org",agent="fixture-browser";
        string packed=(string)encode.Invoke(null,new object[]{cookie,agent});
        object parsed=decode.Invoke(null,new object[]{packed});
        Check(parsed!=null&&(string)session.GetField("Cookie").GetValue(parsed)==cookie&&(string)session.GetField("UserAgent").GetValue(parsed)==agent,"saved session preserves both fields without entering settings");
        foreach(string bad in new[]{"session=fixture\r\nInjected: fixture","Cookie: session=fixture","no-pair","session=fixture; invalid name=value"}){
            bool rejected=false;try{encode.Invoke(null,new object[]{bad,agent});}catch(System.Reflection.TargetInvocationException e){rejected=e.InnerException is ArgumentException;}
            Check(rejected,"invalid Cookie header accepted");
        }
        var type=typeof(ProtectedQuotaCredentials);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
        // In-memory native DPAPI only: do not create/read/replace the real Jev credential file.
        foreach(int length in new[]{32,32750}){
            string value=(string)encode.Invoke(null,new object[]{"session="+new string('x',length),agent});
            byte[] blob=(byte[])type.GetMethod("Protect",flags).Invoke(null,new object[]{"Jev",value});
            try{Check((string)type.GetMethod("Unprotect",flags).Invoke(null,new object[]{"Jev",blob})==value,"session DPAPI round trip must support the full bounded Cookie header");}
            finally{Array.Clear(blob,0,blob.Length);}
        }
    }
}
