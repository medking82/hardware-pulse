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
}
