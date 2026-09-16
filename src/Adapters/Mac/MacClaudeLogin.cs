using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HardwarePulse {
    public sealed class MacClaudeLogin {
        readonly Func<string> environmentToken;
        readonly Func<CancellationToken,string> fileToken;
        readonly MacClaudeKeychain keychain;
        readonly string service,account;
        public MacClaudeLogin(MacClaudeKeychain keychain,Func<string> environmentToken,Func<CancellationToken,string> fileToken,string configDirectory,string home,string account) {
            this.keychain=keychain??throw new ArgumentNullException(nameof(keychain));
            this.environmentToken=environmentToken??throw new ArgumentNullException(nameof(environmentToken));
            this.fileToken=fileToken??throw new ArgumentNullException(nameof(fileToken));
            this.account=account;service=ServiceName(configDirectory,home);
        }
        public static MacClaudeLogin Default() {
            if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("macOS login required");
            var file=ClaudeFileLogin.Default();
            return new MacClaudeLogin(new MacClaudeKeychain(),()=>Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN"),file.Read,
                Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.UserName);
        }
        public static string ServiceName(string configDirectory,string home) {
            const string prefix="Claude Code-credentials";
            if(string.IsNullOrWhiteSpace(configDirectory))return prefix;
            string path=configDirectory=="~"?home:configDirectory.StartsWith("~/",StringComparison.Ordinal)?home.TrimEnd('/')+configDirectory.Substring(1):configDirectory;
            if(!path.StartsWith("/",StringComparison.Ordinal))throw new QuotaFailure("Login unavailable");
            string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.Normalize(NormalizationForm.FormC)))).ToLowerInvariant();
            return prefix+"-"+hash.Substring(0,8);
        }
        public string Read(CancellationToken cancel) {
            cancel.ThrowIfCancellationRequested();
            string token=environmentToken();if(!string.IsNullOrEmpty(token))return token;
            byte[] data=keychain.Read(service,account,cancel);
            if(data==null)return fileToken(cancel);
            try{return ClaudeFileLogin.ParseCredential(data,cancel);}
            finally{CryptographicOperations.ZeroMemory(data);}
        }
    }
}
