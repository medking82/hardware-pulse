using System;
using System.IO;
using System.Threading;

static class CollectorRecoveryFixture {
    static int Main(string[] args){
        if(args.Length!=1)return 2;
        string state=Path.GetFullPath(args[0]),counter=Path.Combine(state,"runs.txt");
        Directory.CreateDirectory(state);
        int runs=File.Exists(counter)?int.Parse(File.ReadAllText(counter))+1:1;
        string pending=counter+".tmp";File.WriteAllText(pending,runs.ToString());
        if(File.Exists(counter))File.Replace(pending,counter,null);else File.Move(pending,counter);
        if(runs==1)return unchecked((int)0xc0000005);
        DateTime deadline=DateTime.UtcNow.AddMinutes(3);
        while(DateTime.UtcNow<deadline&&!File.Exists(Path.Combine(state,"STOP"))){
            File.WriteAllText(Path.Combine(state,"heartbeat.txt"),DateTimeOffset.UtcNow.ToString("o"));
            Thread.Sleep(250);
        }
        return 0;
    }
}
