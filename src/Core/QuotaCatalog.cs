using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HardwarePulse {
    public enum QuotaMetric { Percentage, Money }
    public enum QuotaAuthentication { ExistingLogin, LocalService, ApiKey }
    public sealed class QuotaProviderInfo {
        public readonly string Id,Icon,Accent;
        public readonly QuotaMetric Metric;
        public readonly QuotaAuthentication Authentication;
        public QuotaProviderInfo(string id,string icon,string accent,QuotaMetric metric,QuotaAuthentication authentication=QuotaAuthentication.ExistingLogin){Id=id;Icon=icon;Accent=accent;Metric=metric;Authentication=authentication;}
    }
    // Presentation metadata only. Provider-specific credentials/HTTP stay in adapters.
    public static class QuotaCatalog {
        static readonly ReadOnlyCollection<QuotaProviderInfo> entries=Array.AsReadOnly(new[]{
            new QuotaProviderInfo("Codex","codex","#A5E7D5",QuotaMetric.Percentage),
            new QuotaProviderInfo("Antigravity","antigravity","#A7CBFF",QuotaMetric.Percentage,QuotaAuthentication.LocalService),
            new QuotaProviderInfo("Claude","claude","#E7B497",QuotaMetric.Percentage),
            new QuotaProviderInfo("DeepSeek","deepseek","#94A3F0",QuotaMetric.Money,QuotaAuthentication.ApiKey)
        });
        public static IReadOnlyList<QuotaProviderInfo> Entries {get{return entries;}}
        public static QuotaProviderInfo Find(string id){return entries.FirstOrDefault(p=>p.Id==id);}
        public static string[] Ids {get{return entries.Select(p=>p.Id).ToArray();}}
    }
    public sealed class QuotaBalance {
        public string Currency;
        public decimal Total,Granted,ToppedUp;
        public string Format(decimal amount){return Currency+" "+amount.ToString("0.00####",System.Globalization.CultureInfo.CurrentCulture);}
    }
}
