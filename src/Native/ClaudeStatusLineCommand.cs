using System;
using System.IO;

namespace HardwarePulse {
    public static class ClaudeStatusLineCommand {
        public static string Create(string executable,bool powershell){
            if(string.IsNullOrWhiteSpace(executable)||executable.IndexOfAny(new[]{'\r','\n','\0'})>=0||!Path.IsPathRooted(executable)||Path.GetPathRoot(executable).Length<3)throw new ArgumentException("An absolute executable path is required");
            string path=Path.GetFullPath(executable).Replace('\\','/');
            return powershell?"& '"+path.Replace("'","''")+"' --claude-statusline":"'"+path.Replace("'","'\"'\"'")+"' --claude-statusline";
        }
    }
}
