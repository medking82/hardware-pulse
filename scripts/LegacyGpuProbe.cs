using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using LibreHardwareMonitor.Hardware;
// Development-only live probe. No collector, settings file, installer or task mutation.
// Passing on a modern OS does not establish legacy driver or Windows 7 support.
class LegacyGpuProbe {
 sealed class TransientSettings:ISettings {
  public bool Contains(string name){return false;}
  public string GetValue(string name,string value){return value;}
  public void SetValue(string name,string value){}
  public void Remove(string name){}
 }
 static int Main(string[] args){
  string lib=Path.GetFullPath(args[0]);
  AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string p=Path.Combine(lib,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
  try{return Run();}catch(Exception e){Console.WriteLine(e);return 1;}
 }
 static int Run(){
  var timer=Stopwatch.StartNew();var computer=new Computer(new TransientSettings()){IsGpuEnabled=true};
  try{
   computer.Open();int values=0,temperature=0,fans=0;
   foreach(var h in computer.Hardware){
    if(h.HardwareType!=HardwareType.GpuNvidia&&h.HardwareType!=HardwareType.GpuAmd)throw new Exception("Unexpected hardware group");
    h.Update();
    foreach(var s in h.Sensors){
     if(s.Control!=null&&s.Control.ControlMode!=ControlMode.Undefined)throw new Exception("Hardware control enabled");
     if(s.Value.HasValue){values++;if(s.SensorType==SensorType.Temperature)temperature++;if(s.SensorType==SensorType.Fan)fans++;}
    }
   }
   if(Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Any(m=>m.ModuleName.StartsWith("PawnIO",StringComparison.OrdinalIgnoreCase)))throw new Exception("PawnIO loaded");
   Console.WriteLine("GPU-only probe: devices="+computer.Hardware.Count+" values="+values+" temperatures="+temperature+" fans="+fans+" elapsedMs="+timer.ElapsedMilliseconds+"; controls undefined, no PawnIO module");
   return 0;
  }finally{computer.Close();}
 }
}