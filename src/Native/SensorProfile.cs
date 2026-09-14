using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HardwarePulse {
    public static class SensorProfile {
        public sealed class Spec {
            public string Id,Type;public double Min,Max;
            public Spec(string id,string type,double min,double max){Id=id;Type=type;Min=min;Max=max;}
        }
        public static readonly Dictionary<string,Spec> Specs=new Dictionary<string,Spec> {
            {"cpu",new Spec("/amdcpu/0/temperature/2","Temperature",1,110)},
            {"cpuLoad",new Spec("/amdcpu/0/load/0","Load",0,100)},
            {"vcore",new Spec("/lpc/nct6687dr/0/voltage/4","Voltage",.1,2)},
            {"cpuFan",new Spec("/lpc/nct6687dr/0/fan/0","Fan",0,10000)},
            {"gpu",new Spec("/gpu-nvidia/0/temperature/0","Temperature",1,110)},
            {"gpuLoad",new Spec("/gpu-nvidia/0/load/0","Load",0,100)},
            {"vram",new Spec("/gpu-nvidia/0/temperature/3","Temperature",1,120)},
            {"gpuVolt",new Spec("/gpu-nvidia/0/voltage/0","Voltage",.1,2)},
            {"gpuFan",new Spec("/gpu-nvidia/0/fan/1","Fan",0,10000)},
            {"gpuFan2",new Spec("/gpu-nvidia/0/fan/2","Fan",0,10000)},
            {"ramA",new Spec("/memory/dimm/1/temperature/0","Temperature",1,100)},
            {"ramB",new Spec("/memory/dimm/3/temperature/0","Temperature",1,100)},
            {"system",new Spec("/lpc/nct6687dr/0/temperature/1","Temperature",1,100)},
            {"bottom",new Spec("/lpc/nct6687dr/0/fan/10","Fan",0,10000)},
            {"top",new Spec("/lpc/nct6687dr/0/fan/12","Fan",0,10000)},
            {"diskC",new Spec("/nvme/0/temperature/0","Temperature",1,100)},
            {"diskD",new Spec("/nvme/1/temperature/0","Temperature",1,100)}
        };
        // Only internal, fixed discovery patterns enter this cache. Keep them alive
        // across polls instead of cycling through Regex's small shared static cache.
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string,Regex> matchers=new System.Collections.Concurrent.ConcurrentDictionary<string,Regex>();
        static bool Match(string text,string pattern){
            string key=System.Globalization.CultureInfo.CurrentCulture.Name+"|"+pattern;
            Regex matcher;if(!matchers.TryGetValue(key,out matcher))matcher=matchers.GetOrAdd(key,new Regex(pattern,RegexOptions.IgnoreCase));
            return matcher.IsMatch(text??"");
        }
        static Sensor Find(IEnumerable<Sensor> items,string type,params string[] patterns) {
            foreach(string p in patterns){var matches=items.Where(s=>s.type==type&&Match(s.name,p)).ToArray();if(matches.Length==1)return matches[0];}return null;
        }
        public static Usage MakeUsage(double? used,double? total) {
            if(!used.HasValue||!total.HasValue)return null;
            double u=used.Value,t=total.Value;
            if(double.IsNaN(u)||double.IsNaN(t)||double.IsInfinity(u)||double.IsInfinity(t)||u<0||t<=0||u>t)return null;
            return new Usage {used=u,total=t,percent=100*u/t};
        }
        public static Reading Read(string path,DateTimeOffset now) {
            try{return Parse(Json.Serializer().Deserialize<RawSnapshot>(Json.Read(path)),now);}
            catch(Exception e){return new Reading {error=e.Message};}
        }
        public static Reading Parse(RawSnapshot raw,DateTimeOffset now) {
            if(raw==null||(raw.schema!=1&&raw.schema!=2))throw new FormatException("Unsupported snapshot schema");
            var stamp=DateTimeOffset.Parse(raw.time);double age=(now-stamp).TotalSeconds;
            var result=new Reading {time=stamp};
            if(age< -2||age>15){result.state="STALE";return result;}
            var items=raw.sensors??new Sensor[0];
            var chosen=new Dictionary<string,Sensor>();
            foreach(string key in Specs.Keys)chosen[key]=null;
            if(raw.schema==1){
                foreach(var entry in Specs){var matches=items.Where(s=>s.id==entry.Value.Id&&s.type==entry.Value.Type).ToArray();if(matches.Length==1)chosen[entry.Key]=matches[0];}
                if(chosen["diskC"]!=null&&chosen["diskC"].hardware!="KIOXIA-EXCERIA PLUS G4 SSD")chosen["diskC"]=null;
                if(chosen["diskD"]!=null&&chosen["diskD"].hardware!="KIOXIA-EXCERIA BASIC SSD")chosen["diskD"]=null;
                result.gpuFanCount=1;
            }else{
                var cpuId=items.Where(s=>s.hardwareType=="Cpu").Select(s=>s.hardwareId).Distinct().OrderBy(s=>s,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                var cpu=items.Where(s=>cpuId!=null&&s.hardwareId==cpuId).ToArray();
                chosen["cpu"]=Find(cpu,"Temperature",@"^Core \(Tctl/Tdie\)$",@"^CPU \(Tctl/Tdie\)$","^CPU Package$",@"^Core \(Tdie\)$");
                chosen["cpuLoad"]=Find(cpu,"Load","^CPU Total$");
                if(cpu.Length>0)result.names["CPU"]=cpu[0].hardware;
                var group=items.Where(s=>Match(s.hardwareType,"^Gpu")).GroupBy(s=>s.hardwareId)
                    .OrderBy(g=>g.First().hardwareType=="GpuNvidia"?0:Match(g.First().hardware,@"Radeon\(TM\) Graphics|Intel.*Graphics")?2:1)
                    .ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                var gpu=group==null?new Sensor[0]:group.ToArray();bool intel=gpu.Length>0&&gpu[0].hardwareType=="GpuIntel";
                chosen["gpu"]=Find(gpu,"Temperature","^GPU Core$","^GPU Temperature$");
                chosen["gpuLoad"]=Find(gpu,"Load","^GPU Core$");
                if(chosen["gpuLoad"]==null&&intel)chosen["gpuLoad"]=Find(gpu,"Load","^D3D 3D$");
                chosen["vram"]=Find(gpu,"Temperature","^GPU Memory Junction$","^GPU Memory$");
                chosen["gpuVolt"]=Find(gpu,"Voltage","^GPU Core Voltage$","^GPU Core$");
                chosen["gpuFan"]=Find(gpu,"Fan","^GPU Fan 1$","^GPU Fan$","^GPU$");
                chosen["gpuFan2"]=Find(gpu,"Fan","^GPU Fan 2$");
                if(gpu.Length>0)result.names["GPU"]=gpu[0].hardware;
                var board=items.Where(s=>s.hardwareType=="SuperIO").ToArray();
                chosen["vcore"]=Find(board,"Voltage","^Vcore$","^CPU VCore$");
                chosen["cpuFan"]=Find(board,"Fan","^CPU Fan$","^CPU$");
                chosen["system"]=Find(board,"Temperature","^System$","^Motherboard$");
                var fans=board.Where(s=>s.type=="Fan"&&(chosen["cpuFan"]==null||s.id!=chosen["cpuFan"].id)&&s.value.HasValue).OrderBy(s=>s.id,StringComparer.OrdinalIgnoreCase).ToArray();
                if(Match(raw.boardName,"B850M MORTAR")){
                    chosen["bottom"]=fans.FirstOrDefault(s=>s.id=="/lpc/nct6687dr/0/fan/10");chosen["top"]=fans.FirstOrDefault(s=>s.id=="/lpc/nct6687dr/0/fan/12");
                }else{chosen["bottom"]=fans.ElementAtOrDefault(0);chosen["top"]=fans.ElementAtOrDefault(1);}
                result.names["Airflow"]=string.IsNullOrEmpty(raw.boardName)?"Motherboard":raw.boardName;
                string memory=string.IsNullOrEmpty(raw.memoryName)?"Memory":raw.memoryName;
                var modules=raw.memoryModules??new MemoryModule[0];
                var brands=modules.Select(m=>((m.brand??"")+" "+(m.part??"")).Trim()).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var slots=modules.Select(m=>Regex.Replace(m.slot??"","^DIMM","",RegexOptions.IgnoreCase)).Where(s=>s.Length>0).ToArray();
                if(brands.Length>0)memory+="\n"+string.Join(" / ",brands);
                if(slots.Length>0)memory+=" · Slots "+string.Join(" / ",slots);result.names["Memory"]=memory;
                var dimms=items.Where(s=>Match(s.id,"^/memory/dimm/")&&s.type=="Temperature"&&Match(s.name,@"^DIMM #\d+$")).GroupBy(s=>s.hardwareId).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
                for(int i=0;i<dimms.Length;i++){var candidates=dimms[i].ToArray();if(candidates.Length==1){string key=i==0?"ramA":"ramB";chosen[key]=candidates[0];result.names[key]=Regex.Replace(candidates[0].name,"^DIMM","SPD",RegexOptions.IgnoreCase);}}
                var disks=items.Where(s=>Match(s.id,"^/nvme/")).GroupBy(s=>s.hardwareId).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
                for(int i=0;i<disks.Length;i++){
                    string key=i==0?"diskC":"diskD",model=disks[i].First().hardware??"";
                    chosen[key]=Find(disks[i],"Temperature","^Temperature$","^Composite$","^Composite Temperature$");result.names[key]=model;
                    var matches=(raw.disks??new DiskInfo[0]).Where(d=>string.Equals(Regex.Replace(d.model??"","[^a-zA-Z0-9]",""),Regex.Replace(model,"[^a-zA-Z0-9]",""),StringComparison.OrdinalIgnoreCase)).ToArray();
                    if(matches.Length==1&&matches[0].volumes!=null&&matches[0].volumes.Length>0)result.names[key]=string.Join(" / ",matches[0].volumes)+"  "+Regex.Replace(Regex.Replace(model,"^KIOXIA-EXCERIA "," ",RegexOptions.IgnoreCase)," SSD$","",RegexOptions.IgnoreCase).Trim();
                }
                foreach(string key in new[]{"cpuFan","bottom","top"})if(chosen[key]!=null)result.names[key]=chosen[key].name;
                if(raw.ramUsage!=null){var usage=MakeUsage(raw.ramUsage.usedGb,raw.ramUsage.totalGb);if(usage!=null)result.usage["ram"]=usage;}
                Sensor used=Find(gpu,"SmallData","^GPU Memory Used$"),total=Find(gpu,"SmallData","^GPU Memory Total$");
                Usage vram=used!=null&&total!=null?MakeUsage(used.value/1024,total.value/1024):null;
                if(vram==null&&intel){used=Find(gpu,"SmallData","^D3D Shared Memory Used$");total=Find(gpu,"SmallData","^D3D Shared Memory Total$");vram=used!=null&&total!=null?MakeUsage(used.value/1024,total.value/1024):null;if(vram!=null)vram.label="Shared GPU memory";}
                if(vram!=null)result.usage["vram"]=vram;
                result.gpuFanCount=chosen["gpuFan2"]!=null?2:chosen["gpuFan"]!=null?1:0;
                result.available=chosen.ToDictionary(p=>p.Key,p=>p.Value!=null);
            }
            foreach(var entry in Specs){var sensor=chosen[entry.Key];var spec=entry.Value;if(sensor==null||sensor.type!=spec.Type||!sensor.value.HasValue)continue;double v=sensor.value.Value;if(!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=spec.Min&&v<=spec.Max)result.values[entry.Key]=v;}
            // Show one adapter, never sum virtual/physical counters that may represent
            // the same traffic twice. The selected adapter name is visible in the card.
            var network=items.Where(s=>s.hardwareType=="Network"&&s.type=="Throughput"&&s.value.HasValue&&!double.IsNaN(s.value.Value)&&!double.IsInfinity(s.value.Value)&&s.value.Value>=0)
                .GroupBy(s=>s.hardwareId).OrderByDescending(g=>g.Sum(s=>s.value.Value)).ThenBy(g=>g.Key,StringComparer.Ordinal).FirstOrDefault();
            if(network!=null){
                result.names["Network"]=network.First().hardware;
                var link=(raw.networkLinks??new NetworkLink[0]).FirstOrDefault(n=>n!=null&&n.hardwareId==network.Key);
                if(link!=null){if(!link.connected)result.values["netLink"]=0;else if(link.bitsPerSecond>0)result.values["netLink"]=link.bitsPerSecond.Value;}
                foreach(var direction in new[]{new[]{"netDown","Download Speed"},new[]{"netUp","Upload Speed"}}){
                    var sensor=network.FirstOrDefault(s=>s.name==direction[1]);
                    if(result.available!=null)result.available[direction[0]]=sensor!=null;
                    if(sensor!=null)result.values[direction[0]]=sensor.value.Value;
                }
            }
            result.state="LIVE";result.identity=raw.pid+":"+raw.sequence;return result;
        }
    }
}
