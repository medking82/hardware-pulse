using System;
using System.Collections.Generic;
using System.Globalization;
using HardwarePulse;

internal static class CoreSettingsTests {
    static void Check(bool value,string message){if(!value)throw new Exception("Core settings: "+message);}
    public static void Run(){
        var data=new Dictionary<string,object>{{"unknown",new object()},{"text","value"},{"flag",true},{"number","42.5"}};
        var settings=new SettingsValues(data);var unknown=data["unknown"];
        Check(ReferenceEquals(settings.Data,data),"host map identity preserved");
        Check(settings.Text("text")=="value"&&settings.Text("flag","fallback")=="fallback"&&settings.Text("missing")=="","text typing and fallback");
        Check(settings.Flag("flag")&&settings.Flag("text",true)&&!settings.Flag("missing"),"strict bool and fallback");
        var previous=CultureInfo.CurrentCulture;try{
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
            Check(settings.Number("number",0,0,100)==42.5,"numeric strings use invariant parsing");
            data["number"]=200;Check(settings.Number("number",0,0,100)==100,"maximum clamp");
            data["number"]=-5.0;Check(settings.Number("number",0,0,100)==0,"minimum clamp");
            foreach(var invalid in new object[]{null,"invalid",true,double.NaN,double.PositiveInfinity}){
                data["number"]=invalid;Check(settings.Number("number",150,0,100)==150,"fallback remains unchanged, even outside bounds");
            }
        }finally{CultureInfo.CurrentCulture=previous;}
        var map=settings.Map("nested");map["keep"]=7;
        Check(ReferenceEquals(map,settings.Map("nested"))&&(int)settings.Map("nested")["keep"]==7,"Map shares existing dictionary");
        data["nested"]="invalid";Check(settings.Map("nested").Count==0&&data["nested"] is Dictionary<string,object>,"Map replaces invalid shape");
        data["cardOrder"]=new object[]{"gpu",3,"cpu",null,"gpu"};var order=settings.Order();
        Check(order.Length==3&&order[0]=="gpu"&&order[1]=="cpu"&&order[2]=="gpu","Order filters types without deduplication");
        data["cardOrder"]="cpu";Check(settings.Order().Length==0,"string is not a list of card names");
        Check(ReferenceEquals(data["unknown"],unknown),"unknown data preserved");
        Check(new SettingsValues(null).Data.Count==0,"empty input");
        Console.WriteLine("PASS Core settings: type/fallback/clamp rules, invariant numeric coercion, shared maps, order and unknown fields");
    }
}