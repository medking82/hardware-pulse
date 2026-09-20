using System;
using System.IO;
using System.Threading;

namespace HardwarePulse {
    // Existing current-user login only. No writes, CLI execution or token refresh.
    public sealed class ClaudeFileLogin {
        readonly string path;
        readonly Func<string> environmentToken;
        public ClaudeFileLogin(string path,Func<string> environmentToken) {
            this.path=path??throw new ArgumentNullException(nameof(path));
            this.environmentToken=environmentToken??throw new ArgumentNullException(nameof(environmentToken));
        }
        public static ClaudeFileLogin Default() {
            string root=Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if(string.IsNullOrWhiteSpace(root))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude");
            else if(root=="~")root=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            else if(root.StartsWith("~/",StringComparison.Ordinal))root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),root.Substring(2));
            return new ClaudeFileLogin(Path.Combine(root,".credentials.json"),()=>Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN"));
        }
        public string Read(CancellationToken cancel) {
            cancel.ThrowIfCancellationRequested();
            string supplied=environmentToken();if(!string.IsNullOrEmpty(supplied))return supplied;
            try {
                using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,8192,true);
                if(input.Length>1048576)throw new QuotaFailure("Login unavailable");
                var login=QuotaJson.ReadGraph(input,cancel).GetAwaiter().GetResult();
                cancel.ThrowIfCancellationRequested();
                return QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(login,"claudeAiOauth")??login,"accessToken"));
            }catch(FileNotFoundException){throw new QuotaFailure("Login required");}
            catch(DirectoryNotFoundException){throw new QuotaFailure("Login required");}
        }
        public static string ParseCredential(byte[] bytes,CancellationToken cancel) {
            if(bytes==null||bytes.Length==0||bytes.Length>1048576)throw new QuotaFailure("Login unavailable");
            cancel.ThrowIfCancellationRequested();
            using var stream=new MemoryStream(bytes,false);
            var login=QuotaJson.ReadGraph(stream,cancel).GetAwaiter().GetResult();
            return QuotaDecoder.Text(QuotaDecoder.Get(QuotaDecoder.Get(login,"claudeAiOauth")??login,"accessToken"));
        }
    }
}
