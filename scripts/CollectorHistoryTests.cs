using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardwarePulse;
using LibreHardwareMonitor.Hardware;

static class CollectorHistoryTests {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}

    sealed class FakeSensor : ISensor {
        readonly Identifier identifier;
        readonly IHardware hardware;
        readonly List<SensorValue> values=new List<SensorValue>{new SensorValue(11,DateTime.UtcNow)};
        public FakeSensor(IHardware hardware,string id,string name,SensorType type,float? value){this.hardware=hardware;identifier=new Identifier(id.Split('/'));Name=name;SensorType=type;Value=value;ValuesTimeWindow=TimeSpan.FromDays(1);}
        public IControl Control {get{return null;}}
        public IHardware Hardware {get{return hardware;}}
        public Identifier Identifier {get{return identifier;}}
        public int Index {get{return 0;}}
        public bool IsDefaultHidden {get{return false;}}
        public float? Max {get;private set;}
        public float? Min {get;private set;}
        string name;
        public string Name {get{return name;}set{name=value;}}
        public IReadOnlyList<IParameter> Parameters {get{return new IParameter[0];}}
        public SensorType SensorType {get;private set;}
        public float? Value {get;set;}
        public IEnumerable<SensorValue> Values {get{return values;}}
        public TimeSpan ValuesTimeWindow {get;set;}
        public void ResetMin(){Min=null;}
        public void ResetMax(){Max=null;}
        public void ClearValues(){values.Clear();}
        public void Accept(IVisitor visitor){visitor.VisitSensor(this);}
        public void Traverse(IVisitor visitor){foreach(var parameter in Parameters)parameter.Accept(visitor);}
    }

    sealed class FakeHardware : IHardware {
        readonly Identifier identifier;
        readonly List<ISensor> sensors=new List<ISensor>();
        IHardware[] children;
        readonly Action<FakeHardware> onUpdate;
        public FakeHardware(string id,string name,HardwareType type,IHardware[] children,Action<FakeHardware> onUpdate=null){identifier=new Identifier(id);Name=name;HardwareType=type;this.children=children??new IHardware[0];this.onUpdate=onUpdate;}
        public HardwareType HardwareType {get;private set;}
        public Identifier Identifier {get{return identifier;}}
        public string Name {get;set;}
        public IHardware Parent {get{return null;}}
        public ISensor[] Sensors {get{return sensors.ToArray();}}
        public IHardware[] SubHardware {get{return children;}}
        public IDictionary<string,string> Properties {get{return new Dictionary<string,string>();}}
#pragma warning disable 0067
        public event SensorEventHandler SensorAdded;
        public event SensorEventHandler SensorRemoved;
#pragma warning restore 0067
        public string GetReport(){return "fake";}
        public void Update(){if(onUpdate!=null)onUpdate(this);}
        public void Add(ISensor sensor){sensors.Add(sensor);}
        public void SetChildren(IHardware[] value){children=value??new IHardware[0];}
        public void Accept(IVisitor visitor){visitor.VisitHardware(this);}
        public void Traverse(IVisitor visitor){foreach(var sensor in sensors)sensor.Accept(visitor);foreach(var child in children)child.Traverse(visitor);}
    }

    sealed class EmptySettings : ISettings {
        public bool Contains(string name){return false;}
        public void SetValue(string name,string value){}
        public string GetValue(string name,string value){return value;}
        public void Remove(string name){}
    }

    sealed class TestHardware : Hardware {
        public TestHardware():base("Real test hardware",new Identifier("real","test"),new EmptySettings()){}
        public override HardwareType HardwareType {get{return HardwareType.Motherboard;}}
        public override void Update(){}
        public void Add(ISensor sensor){ActivateSensor(sensor);}
    }

    static void InvokeReadSensors(IHardware hardware,List<HardwarePulse.Sensor> output){
        var method=typeof(Collector).GetMethod("ReadSensors",BindingFlags.Static|BindingFlags.NonPublic);
        Check(method!=null,"Collector.ReadSensors was not found");
        method.Invoke(null,new object[]{hardware,output});
    }

    static void RealSensorContract(){
        var hardware=new TestHardware();
        var sensorType=typeof(IHardware).Assembly.GetType("LibreHardwareMonitor.Hardware.Sensor",true);
        var sensor=(ISensor)Activator.CreateInstance(sensorType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{"Real load",0,SensorType.Load,hardware,new EmptySettings()},null);
        var setter=sensorType.GetProperty("Value",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetSetMethod(true);
        for(int i=1;i<=16;i++)setter.Invoke(sensor,new object[]{(float)i});
        Check(sensor.ValuesTimeWindow>TimeSpan.Zero&&sensor.Values.Any(),"Real LHM sensor did not retain seeded history");
        hardware.Add(sensor);var output=new List<HardwarePulse.Sensor>();InvokeReadSensors(hardware,output);
        Check(sensor.ValuesTimeWindow==TimeSpan.Zero&&!sensor.Values.Any(),"Collector did not disable real LHM sensor history");
        Check(sensor.Value==16&&sensor.Min==1&&sensor.Max==16,"Collector changed real LHM current/min/max values");
        setter.Invoke(sensor,new object[]{17f});
        Check(!sensor.Values.Any()&&sensor.Value==17&&sensor.Min==1&&sensor.Max==17,"Real LHM sensor kept history or lost min/max after disabling");
        Check(output.Count==1&&output[0].value==16&&output[0].name=="Real load"&&output[0].type=="Load","Real LHM DTO changed current value or metadata");
    }

    static void Main(){
        FakeHardware child=null;
        var root=new FakeHardware("fake/root","Root board",HardwareType.Motherboard,new IHardware[0],h=>{
            if(h.Sensors.Length==0)h.Add(new FakeSensor(h,"fake/root/load","Root load",SensorType.Load,42));
            if(child==null){child=new FakeHardware("fake/child","Child device",HardwareType.Storage,new IHardware[0],c=>{if(c.Sensors.Length==0)c.Add(new FakeSensor(c,"fake/child/temp","Child temperature",SensorType.Temperature,null));});h.SetChildren(new IHardware[]{child});}
        });
        // The root's child is intentionally added during the root Update; the next
        // recursive walk must still see its sensor and apply the same policy.
        var output=new List<HardwarePulse.Sensor>();InvokeReadSensors(root,output);
        Check(output.Count==2,"Dynamic root/child sensors were not published");
        Check(output.Any(s=>s.id=="/fake/root/load"&&s.value==42&&s.name=="Root load"&&s.hardware=="Root board"&&s.type=="Load"),"Root sensor metadata or current value changed");
        Check(output.Any(s=>s.id=="/fake/child/temp"&&s.value==null&&s.name=="Child temperature"&&s.hardware=="Child device"&&s.type=="Temperature"),"Child null value or metadata changed");
        Check(root.Sensors.Cast<FakeSensor>().All(s=>s.ValuesTimeWindow==TimeSpan.Zero),"Root sensor history was not disabled");
        Check(child.Sensors.Cast<FakeSensor>().All(s=>s.ValuesTimeWindow==TimeSpan.Zero),"Child sensor history was not disabled");
        RealSensorContract();
        Console.WriteLine("PASS collector history: dynamic root/child sensors, current/null values and metadata preserved while LHM history is disabled");
    }
}
