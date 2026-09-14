using System;
using System.IO;
using HardwarePulse;

static class MaterialPolicyTests {
    static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    static int Main(string[] args){
        try{
            string path=Path.Combine(args[0],"settings.json");
            Json.WriteAtomic(path,new {opacity=20,positionLocked=true,language="zh-CN",fontSize=14,background="#234567",futureField=new {enabled=true},cardsVisible=new {GPU=false}});
            var settings=new Settings(path);
            double saved=settings.Number("opacity",70,0,100);
            var monitor=new MaterialPolicy(saved,settings.Flag("positionLocked"),false,false,false);
            var page=new MaterialPolicy(saved,true,true,false,false);
            var unlocked=new MaterialPolicy(saved,false,false,false,false);
            Check(monitor.Clear&&monitor.EffectiveOpacity(true)==.05,"Locked monitor should use five percent for saved twenty percent");
            Check(!page.Clear&&page.EffectiveOpacity(true)==.2&&!unlocked.Clear&&unlocked.EffectiveOpacity(true)==.2,"Settings/unlock should restore saved opacity");
            foreach(bool highContrast in new[]{false,true}){
                var opaque=new MaterialPolicy(saved,true,false,!highContrast,highContrast);
                Check(opaque.Solid&&!opaque.CanAdjustOpacity(true)&&opaque.EffectiveOpacity(true)==1,"Solid/high contrast must win over lock");
            }
            Check(!monitor.CanAdjustOpacity(false)&&monitor.EffectiveOpacity(false)==1,"Unsupported backdrop must remain readable");
            var transparent=new MaterialPolicy(0,false,true,false,false);
            Check(transparent.Clear&&transparent.EffectiveOpacity(true)==0&&transparent.CanAdjustOpacity(true),"Zero opacity should retain transparent background");
            settings.Save();var restored=new Settings(path);
            Check(restored.Number("opacity",70,0,100)==20&&restored.Flag("positionLocked")&&restored.Number("fontSize",12,10,16)==14,"Presentation must not rewrite preferences");
            Check(restored.Text("language")=="zh-CN"&&restored.Text("background")=="#234567"&&(bool)restored.Map("futureField")["enabled"]&&!(bool)restored.Map("cardsVisible")["GPU"],"Roundtrip must preserve unrelated and unknown settings");
            var defaults=new Settings(Path.Combine(args[0],"missing.json"));
            Check(!defaults.Flag("positionLocked")&&defaults.Number("opacity",70,0,100)==70,"Fresh settings defaults");
            Console.WriteLine("PASS material policy: lock/settings/unlock, solid/high contrast, unsupported/zero opacity and preference compatibility");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
