using System;
using System.Threading;
using System.Threading.Tasks;

namespace HardwarePulse {
    // Single host-thread owner; at most one I/O task, including retiring requests.
    public sealed class LocalInferenceSession : IDisposable {
        readonly Func<string,string,CancellationToken,LocalInferenceReading> read;
        Task<LocalInferenceReading> pending;CancellationTokenSource cancel;
        LocalInferenceReading reading;
        string provider="Strata",endpoint="";bool enabled,disposed;long revision,pendingRevision;
        DateTimeOffset next,started;
        public LocalInferenceSession(Func<string,string,CancellationToken,LocalInferenceReading> read){this.read=read;}
        public void Configure(bool enabled,string provider,string endpoint){
            if(disposed||this.enabled==enabled&&this.provider==provider&&this.endpoint==endpoint)return;
            this.enabled=enabled;this.provider=provider;this.endpoint=endpoint;revision++;reading=null;next=default(DateTimeOffset);
            if(cancel!=null)cancel.Cancel();
        }
        public LocalInferenceReading Current(DateTimeOffset now){
            if(disposed||!enabled)return new LocalInferenceReading{Provider=provider,Status="Off",Observed=now};
            if(pending!=null&&(now-started).TotalSeconds>3)return new LocalInferenceReading{Provider=provider,Observed=now};
            return reading!=null&&reading.Fresh(now)?reading:new LocalInferenceReading{Provider=provider,Observed=now};
        }
        public void Tick(DateTimeOffset now){
            if(disposed)return;
            if(pending!=null){
                if(!pending.IsCompleted)return;
                var done=pending;pending=null;cancel.Dispose();cancel=null;
                if(enabled&&pendingRevision==revision){
                    reading=done.Status==TaskStatus.RanToCompletion?done.Result:null;
                    next=now.AddSeconds(reading!=null&&reading.Status!="Unavailable"?2:10);
                }
            }
            if(!enabled||pending!=null||now<next)return;
            string selectedProvider=provider,selectedEndpoint=endpoint;pendingRevision=revision;started=now;
            cancel=new CancellationTokenSource();var token=cancel.Token;
            pending=Task.Run(()=>{try{return read(selectedProvider,selectedEndpoint,token);}catch{return new LocalInferenceReading{Provider=selectedProvider,Observed=now};}});
        }
        public void Dispose(){if(disposed)return;disposed=true;reading=null;if(cancel!=null){cancel.Cancel();var retiring=cancel;cancel=null;if(pending!=null)pending.ContinueWith(task=>retiring.Dispose(),TaskScheduler.Default);else retiring.Dispose();}}
    }
}
