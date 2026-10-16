using System;
using System.Globalization;

namespace HardwarePulse {
    // DeepSeek publishes wallet money, not a percentage window. Amounts stay formatted strings
    // so no view can turn a balance into a progress bar, and a malformed row is dropped instead
    // of clearing the whole account balance.
    public static class DeepSeekBalance {
        public static QuotaReading Decode(object body,DateTimeOffset now){
            var reading=new QuotaReading{Provider="DeepSeek",Observed=now};
            foreach(object entry in QuotaDecoder.Items(QuotaDecoder.Get(body,"balance_infos"))){
                string currency=Currency(QuotaDecoder.Text(QuotaDecoder.Get(entry,"currency")));
                if(currency==null)continue;
                string total=Amount(QuotaDecoder.Get(entry,"total_balance"));
                if(total==null)continue; // Row-level validation: one bad row never hides a good one.
                reading.Balances.Add(new QuotaBalance{Label="Available balance",Currency=currency,Amount=total});
                reading.AllBalances.Add(new QuotaBalance{Label="Available balance",Currency=currency,Amount=total});
                Extra(reading,"Granted",currency,QuotaDecoder.Get(entry,"granted_balance"));
                Extra(reading,"Topped up",currency,QuotaDecoder.Get(entry,"topped_up_balance"));
            }
            // An unavailable or empty wallet with a valid row is a real zero balance, not a failure.
            if(reading.Balances.Count>0)reading.Status="Live";
            return reading;
        }
        static void Extra(QuotaReading reading,string label,string currency,object value){string amount=Amount(value);if(amount!=null)reading.AllBalances.Add(new QuotaBalance{Label=label,Currency=currency,Amount=amount});}
        static string Currency(string value){string text=(value??"").Trim().ToUpperInvariant();return text=="CNY"||text=="USD"?text:null;}
        // Only the documented decimal string form is accepted, matching the strict typing of
        // the percentage decoders; a number, NaN or exponent form is a changed contract.
        static string Amount(object value){
            var text=value as string;
            decimal amount;
            if(text==null||!decimal.TryParse(text.Trim(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out amount))return null;
            return Bounded(amount);
        }
        static string Bounded(decimal amount){return amount<0||amount>1000000000000m?null:amount.ToString("0.00",CultureInfo.InvariantCulture);}
    }
}
