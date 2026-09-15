using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace HardwarePulse.Desktop;

// Opt-in investigation only. Counts cover startup, warm-up and the measured window.
// No allocations, stacks, object contents or machine identifiers are recorded.
public sealed class GcDiagnostics : EventListener {
    readonly ConcurrentDictionary<string,int> counts=new();
    protected override void OnEventSourceCreated(EventSource source) {
        if(source.Name=="Microsoft-Windows-DotNETRuntime")EnableEvents(source,EventLevel.Informational,(EventKeywords)1);
    }
    protected override void OnEventWritten(EventWrittenEventArgs data) {
        if(data.EventName!="GCStart_V2"||data.PayloadNames==null||data.Payload==null||counts==null)return;
        string Field(string name) {
            int index=data.PayloadNames.IndexOf(name);
            return index<0?"unknown":Convert.ToString(data.Payload[index],System.Globalization.CultureInfo.InvariantCulture)??"unknown";
        }
        string key=$"depth={Field("Depth")};reason={Field("Reason")};type={Field("Type")}";
        counts.AddOrUpdate(key,1,(_,value)=>value+1);
    }
    public string Report()=>System.Text.Json.JsonSerializer.Serialize(new {
        Scope="entire-process-including-warmup",Collections=counts.OrderBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Value)
    });
}
