namespace HardwarePulse {
    // Returns numeric text only. Views own labels, spacing and unavailable state.
    public static class ReadingFormat {
        public static string SensorNumber(double value,string unit,bool compactVoltage=false){
            unit=unit.Trim();
            string format=unit=="RPM"||unit=="FPS"?"0":unit=="V"?(compactVoltage?"0.#":"F3"):"F1";
            return value.ToString(format);
        }
        public static string UsageText(Usage value){
            return string.Format("{0:F1} / {1:F1} GB · {2:F1}%",value.used,value.total,value.percent);
        }
    }
}
