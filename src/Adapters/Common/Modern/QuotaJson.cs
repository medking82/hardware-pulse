#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    internal static class QuotaJson {
        const int Limit=1048576;
        internal static async Task<object> ReadGraph(Stream stream,CancellationToken cancel){
            using(var memory=new MemoryStream()){
                var buffer=new byte[8192];int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length,cancel).ConfigureAwait(false))>0){
                    if(memory.Length+count>Limit)throw new QuotaFailure("Quota unavailable");
                    memory.Write(buffer,0,count);
                }
                cancel.ThrowIfCancellationRequested();
                var bytes=memory.GetBuffer();int offset=memory.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;
                using(var document=JsonDocument.Parse(bytes.AsMemory(offset,(int)memory.Length-offset),new JsonDocumentOptions {MaxDepth=32})){
                    if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new QuotaFailure("Quota unavailable");
                    return Graph(document.RootElement);
                }
            }
        }
        static object Graph(JsonElement value){
            switch(value.ValueKind){
                case JsonValueKind.Object:
                    var map=new Dictionary<string,object>();foreach(var property in value.EnumerateObject())map[property.Name]=Graph(property.Value);return map;
                case JsonValueKind.Array:
                    var list=new List<object>();foreach(var item in value.EnumerateArray())list.Add(Graph(item));return list;
                case JsonValueKind.String:return value.GetString();
                case JsonValueKind.Number:if(value.TryGetInt64(out long integer))return integer;return value.GetDouble();
                case JsonValueKind.True:return true;
                case JsonValueKind.False:return false;
                default:return null;
            }
        }
    }
}
