static class CiForegroundPreparationTests {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run() {
        var original=Console.Out;using var quiet=new StringWriter();Console.SetOut(quiet);
        try {
            var env=new Dictionary<string,string>{{"GITHUB_ACTIONS","true"},{"PULSE_CI_HOSTED_RUNNER","github-hosted"},{"RUNNER_OS","Windows"},{"RUNNER_ARCH","ARM64"},{"GITHUB_REPOSITORY","medking82/hardware-pulse"},{"PULSE_CI_CXH_CLOSE_APPROVED","1"}};
            string? Read(string key)=>env.GetValueOrDefault(key);
            Check(CiForegroundPreparation.Authorized(Read,true,true),"Approved CI scope rejected");
            Check(!CiForegroundPreparation.Authorized(Read,false,true)&&!CiForegroundPreparation.Authorized(Read,true,false),"Local/non-ARM scope accepted");
            foreach(string key in env.Keys.ToArray()) {string value=env[key];env[key]="wrong";Check(!CiForegroundPreparation.Authorized(Read,true,true),"Missing scope guard: "+key);env[key]=value;}
            foreach(var context in new[]{(Event:"pull_request",Head:"feature/normal-pr",Ref:"refs/pull/3/merge"),(Event:"push",Head:"",Ref:"refs/heads/main"),(Event:"workflow_dispatch",Head:"",Ref:"refs/heads/main")}) {
                env["GITHUB_EVENT_NAME"]=context.Event;env["GITHUB_HEAD_REF"]=context.Head;env["GITHUB_REF"]=context.Ref;
                Check(CiForegroundPreparation.Authorized(Read,true,true),"Regular hosted CI was rejected: "+context.Event);
            }
            var valid=new CiForegroundPreparation.Target((nint)123,42,1,"WWAHost","Windows.UI.Core.CoreWindow","Microsoft.Windows.CloudExperienceHost_10.0.26100.1_neutral_neutral_cw5n1h2txyewy",CiForegroundPreparation.AppId,true,true,true,true);
            int sends=0,waits=0,inspections=0;
            CiForegroundPreparation.Delivery Send(nint window,uint message,uint timeout) {sends++;Check(window==valid.Window&&message==0x10&&timeout==2000,"Close was not one bounded WM_CLOSE");return new(true,0);}
            bool Gone(CiForegroundPreparation.Target target,int timeout){waits++;Check(target==valid&&timeout==3000,"Disappearance verification exceeded policy");return true;}
            Check(CiForegroundPreparation.Run(false,()=>{inspections++;return valid;},Send,Gone)==2&&inspections==0&&sends==0,"Unauthorized environment touched UI");
            foreach(var invalid in new[]{valid with{Window=0},valid with{Window=(nint)0xffff},valid with{Window=(nint)(-1)},valid with{Pid=0},valid with{Created=0},valid with{Process="other"},valid with{Class="other"},valid with{Package="Microsoft.Other_cw5n1h2txyewy"},valid with{Package="Microsoft.Windows.CloudExperienceHost_other_publisher"},valid with{Application="other"},valid with{Foreground=false},valid with{FullDesktop=false},valid with{Visible=false},valid with{Foreign=false}})
                Check(CiForegroundPreparation.Run(true,()=>invalid,Send,Gone)==0&&sends==0&&waits==0,"Unverified target was acted on");
            Check(CiForegroundPreparation.Run(true,()=>null,Send,Gone)==0&&sends==0,"Missing target was acted on");
            inspections=0;
            Check(CiForegroundPreparation.Run(true,()=>++inspections==1?valid:valid with{Created=2},Send,Gone)==1&&sends==0,"Changed target was closed");
            foreach(int error in new[]{5,1460}) {
                sends=waits=0;
                Check(CiForegroundPreparation.Run(true,()=>valid,(w,m,t)=>{sends++;return new(false,error);},Gone)==1&&sends==1&&waits==0,"Denied/timed-out close retried or continued");
            }
            sends=waits=0;
            Check(CiForegroundPreparation.Run(true,()=>valid,Send,(target,timeout)=>{waits++;return false;})==1&&sends==1&&waits==1,"Ignored close retried");
            sends=waits=0;
            Check(CiForegroundPreparation.Run(true,()=>valid,Send,Gone)==0&&sends==1&&waits==1,"Successful one-shot close was not verified");
        } finally {Console.SetOut(original);}
        Console.WriteLine("PASS hosted ARM CI close policy: scope/identity/fullscreen/recheck guards, one bounded WM_CLOSE, no retry after denial/timeout/ignored close; all UI callbacks mocked");
    }
}
