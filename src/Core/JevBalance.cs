using System;
using System.Globalization;

namespace HardwarePulse {
    // TypeSafe's read-only billing action reports integer USD cents, not quota percentages.
    public static class JevBalance {
        public static QuotaReading Decode(object body,DateTimeOffset now){
            var reading=new QuotaReading{Provider="Jev",Observed=now};
            if(!object.Equals(QuotaDecoder.Get(body,"ok"),true))return reading;
            string amount=Amount(QuotaDecoder.Get(QuotaDecoder.Get(body,"data"),"balanceCents"));
            if(amount==null)return reading;
            reading.Balances.Add(new QuotaBalance{Label="Available balance",Currency="USD",Amount=amount});
            reading.AllBalances.Add(new QuotaBalance{Label="Available balance",Currency="USD",Amount=amount});
            reading.Status="Live";return reading;
        }
        static string Amount(object value){
            if(!(value is int||value is long||value is decimal||value is double))return null;
            try{
                decimal cents;
                if(value is double){
                    double number=(double)value;
                    if(double.IsNaN(number)||double.IsInfinity(number)||number!=Math.Truncate(number)||number>9007199254740991d||number< -9007199254740991d)return null;
                    // Double-to-decimal conversion rounds to 15 significant digits. Validate
                    // the binary integer first, then convert through Int64 without losing cents.
                    cents=(decimal)(long)number;
                }else cents=Convert.ToDecimal(value,CultureInfo.InvariantCulture);
                if(cents!=decimal.Truncate(cents)||cents>9007199254740991m||cents< -9007199254740991m)return null;
                return (cents/100m).ToString("0.00",CultureInfo.InvariantCulture);
            }catch(OverflowException){return null;}
        }
    }
}
