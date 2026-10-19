using System;
using System.Collections.Generic;

namespace HardwarePulse {
    // Single registry for every quota provider: display name, icon, accent, metric kind and
    // credential kind. The settings toggles, monitor cards, Desktop rows and diagnostics all
    // read this list, so adding a provider (Grok, Qwen, …) means registering metadata here plus
    // a fixed-endpoint transport branch and a decoder — never a second UI path.
    public sealed class QuotaProvider {
        public string Name,Icon,Accent;
        public bool Money;  // Money balances instead of percentage windows.
        public bool ApiKey; // Needs an API key stored for the current Windows user.
        public bool SessionHeader; // Explicit local web session, never a model API key.
    }
    public static class QuotaCatalog {
        public static readonly QuotaProvider[] All={
            new QuotaProvider{Name="Codex",Icon="codex",Accent="#A5E7D5"},
            new QuotaProvider{Name="Antigravity",Icon="antigravity",Accent="#A7CBFF"},
            new QuotaProvider{Name="Claude",Icon="claude",Accent="#E7B497"},
            // Distinct from Antigravity's blue so both cards stay readable side by side.
            new QuotaProvider{Name="DeepSeek",Icon="deepseek",Accent="#6E8FFF",Money=true,ApiKey=true},
            new QuotaProvider{Name="Jev",Icon="jev",Accent="#A9E4D4",Money=true,SessionHeader=true}
        };
        public static string[] Names(){var names=new List<string>();foreach(QuotaProvider provider in All)names.Add(provider.Name);return names.ToArray();}
        public static QuotaProvider Find(string provider){foreach(QuotaProvider candidate in All)if(string.Equals(candidate.Name,provider,StringComparison.Ordinal))return candidate;return null;}
        public static bool IsMoney(string provider){var found=Find(provider);return found!=null&&found.Money;}
        public static bool NeedsApiKey(string provider){var found=Find(provider);return found!=null&&found.ApiKey;}
        public static bool NeedsSessionHeader(string provider){var found=Find(provider);return found!=null&&found.SessionHeader;}
        public static string Accent(string provider){var found=Find(provider);return found==null?"#A7CBFF":found.Accent;}
    }
}
