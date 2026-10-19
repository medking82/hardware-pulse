using System;
using System.Text;

namespace HardwarePulse {
    // This opaque envelope is still a credential. Only the DPAPI store may persist it.
    public sealed class JevSession {
        public string Cookie,UserAgent;
        public static bool ValidCookie(string cookie){
            if(string.IsNullOrEmpty(cookie)||cookie.Length>32768||cookie.StartsWith("Cookie:",StringComparison.OrdinalIgnoreCase))return false;
            foreach(char c in cookie)if(c<32||c>126)return false;
            foreach(string pair in cookie.Split(';')){
                string item=pair.Trim();int split=item.IndexOf('=');if(split<1)return false;
                for(int index=0;index<split;index++)if(!char.IsLetterOrDigit(item[index])&&"!#$%&'*+-.^_`|~".IndexOf(item[index])<0)return false;
            }
            return true;
        }
        public static bool ValidUserAgent(string agent){if(string.IsNullOrEmpty(agent)||agent.Length>1024)return false;foreach(char c in agent)if(c<32||c>126)return false;return true;}
        public static string Encode(string cookie,string userAgent){
            if(!ValidCookie(cookie)||!ValidUserAgent(userAgent))throw new ArgumentException("Invalid TypeSafe session");
            byte[] bytes=Encoding.UTF8.GetBytes(userAgent+"\n"+cookie);
            try{return Convert.ToBase64String(bytes);}finally{Array.Clear(bytes,0,bytes.Length);}
        }
        public static JevSession Decode(string encoded){
            if(string.IsNullOrEmpty(encoded)||encoded.Length>65536)return null;
            byte[] bytes=null;
            try{
                bytes=Convert.FromBase64String(encoded);if(Convert.ToBase64String(bytes)!=encoded)return null;
                string text=new UTF8Encoding(false,true).GetString(bytes);int split=text.IndexOf('\n');if(split<1)return null;
                string agent=text.Substring(0,split),cookie=text.Substring(split+1);
                return ValidCookie(cookie)&&ValidUserAgent(agent)?new JevSession{Cookie=cookie,UserAgent=agent}:null;
            }catch(FormatException){return null;}catch(ArgumentException){return null;}
            finally{if(bytes!=null)Array.Clear(bytes,0,bytes.Length);}
        }
    }
}
