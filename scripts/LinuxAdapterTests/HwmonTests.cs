using System;
using System.IO;
using System.Linq;
using HardwarePulse;

static class HwmonTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(bool live) {
        string root=Path.Combine(Path.GetTempPath(),"pulse-hwmon-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var empty=new LinuxHwmonReadings(root);
            Check(empty.Channels.Count==0&&empty.Read(DateTimeOffset.UtcNow).state=="OFFLINE","No fabricated sensors");
            string chip=Path.Combine(root,"hwmon0");Directory.CreateDirectory(chip);
            void Write(string name,string value)=>File.WriteAllText(Path.Combine(chip,name),value);
            Write("name","fixture_chip\n");Write("temp1_input","54000\n");Write("temp1_label","Package id 0\n");
            Write("temp2_input","-1250");Write("fan1_input","0");Write("fan2_input","1200");Write("pwm1_input","255");
            Write("temp0_input","90000");Write("temp3_input","2000");Write("temp3_type","4");
            Write("temp1_enable","1");Write("fan2_fault","0");
            var source=new LinuxHwmonReadings(root);var now=DateTimeOffset.UtcNow;
            var first=source.Read(now);
            Check(source.Channels.Count==5&&first.values.Count==4,"Only temperature and fan channels; thermistor unsupported");
            Check(first.values["hwmon0/temp1"]==54&&first.values["hwmon0/temp2"]== -1.25,"Millidegrees conversion, negative temperature");
            Check(first.values["hwmon0/fan1"]==0&&first.values["hwmon0/fan2"]==1200,"Zero RPM is valid");
            Check(source.Channels.Single(x=>x.Id=="hwmon0/temp1").Label=="fixture_chip · Package id 0","Kernel labels preserved without CPU mapping");
            Check(first.time==now&&first.identity!=source.Read(now).identity,"Fresh snapshot identity");
            Write("fan2_fault","1");Write("temp1_enable","0");Write("fan1_input","-1");Write("temp2_input","NaN");
            Check(source.Read(now).values.Count==0,"Fault, disabled, negative fan and malformed input unavailable");
            Write("fan2_fault","0");Write("fan2_input","1300");
            Check(source.Read(now).values["hwmon0/fan2"]==1300,"Fresh reads recover, no stale value");
            File.Delete(Path.Combine(chip,"fan2_input"));
            Check(!source.Read(now).available["hwmon0/fan2"],"Removed channel remains unavailable until refresh");
            Write("fan3_input","700");
            Check(!source.Read(now).values.ContainsKey("hwmon0/fan3"),"Poll does not rediscover tree");
            source.Refresh();Check(source.Read(now).values["hwmon0/fan3"]==700&&!source.Channels.Any(x=>x.Id=="hwmon0/fan2"),"Explicit refresh discovers/removes channels");
            Write("fan3_input",new string('1',257));
            Check(!source.Read(now).available["hwmon0/fan3"],"Bounded attribute read");
            foreach(string bad in new[]{"1.5","9223372036854775808","1200\n1300"}) {
                Write("fan3_input",bad);Check(!source.Read(now).available["hwmon0/fan3"],"Invalid integer is unavailable");
            }
            Write("temp2_input","-273151");
            Check(!source.Read(now).available["hwmon0/temp2"],"Reject below absolute zero");
            Write("fan3_input","800");Write("fan2_fault","unknown");
            Check(source.Read(now).values["hwmon0/fan3"]==800,"Broken neighboring sensors do not suppress healthy channels");
            Check(File.ReadAllText(Path.Combine(chip,"temp1_enable"))=="0","Never enable hardware");
        }finally {Directory.Delete(root,true);}
        if(live) {
            var source=new LinuxHwmonReadings();var sample=source.Read(DateTimeOffset.UtcNow);
            Check(sample.values.Keys.All(id=>source.Channels.Any(x=>x.Id==id)),"Native hwmon values belong to discovered channels");
            Console.WriteLine("PASS Linux native hwmon read (zero exposed channels is valid)");
        }else if(!OperatingSystem.IsLinux()) {
            bool rejected=false;try{new LinuxHwmonReadings();}catch(PlatformNotSupportedException){rejected=true;}
            Check(rejected,"Native hwmon requires Linux");
        }
        Console.WriteLine("PASS Linux hwmon fixtures");
    }
}
