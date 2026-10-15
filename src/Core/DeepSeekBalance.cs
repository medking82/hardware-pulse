using System;
using System.Collections.Generic;
using System.Globalization;

namespace HardwarePulse {
    public static class DeepSeekBalance {
        static bool Amount(object value,out decimal amount){return value is string&&decimal.TryParse((string)value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowExponent,CultureInfo.InvariantCulture,out amount)?true:Fail(out amount);}
        static bool Fail(out decimal amount){amount=0;return false;}
        public static QuotaReading Decode(object body,DateTimeOffset now){
            var result=new QuotaReading{Provider="DeepSeek",Observed=now};
            if(!(QuotaDecoder.Get(body,"is_available") is bool))return result;
            var seen=new HashSet<string>();
            foreach(var row in QuotaDecoder.Items(QuotaDecoder.Get(body,"balance_infos"))){
                string currency=QuotaDecoder.Text(QuotaDecoder.Get(row,"currency"));decimal total,granted,paid;
                // Skip rows this provider shape cannot represent; a malformed row must
                // neither zero the whole account nor poison the rows that parsed.
                if(!Amount(QuotaDecoder.Get(row,"total_balance"),out total)||!Amount(QuotaDecoder.Get(row,"granted_balance"),out granted)||!Amount(QuotaDecoder.Get(row,"topped_up_balance"),out paid)||total<0||granted<0||paid<0||(currency!="CNY"&&currency!="USD")||!seen.Add(currency))continue;
                // A depleted wallet is a balance, not a missing reading.
                result.Balances.Add(new QuotaBalance{Currency=currency,Total=total,Granted=granted,ToppedUp=paid});
            }
            if(result.Balances.Count>0)result.Status="Live";
            return result;
        }
    }
}
