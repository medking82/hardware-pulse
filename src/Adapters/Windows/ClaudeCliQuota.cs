using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HardwarePulse {
    // Explicit/Auto alternative to OAuth HTTP; the session owns the shared request budget.
    // Claude Code owns its login. No prompts, browser cookies or token arguments.
    internal static class ClaudeCliQuota {
        internal static string InstalledExecutable(){
            foreach(string path in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","bin","claude.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","claude","claude.exe")})if(File.Exists(path))return path;
            return null;
        }
        internal static QuotaReading Read(string executable,CancellationToken cancel){
            var environment=new Dictionary<string,string>{{"DISABLE_AUTOUPDATER","1"}};
            return (QuotaReading)QuotaChildProcess.ReadWithDiagnostics(executable,"--safe-mode --strict-mcp-config --tools \"\" --permission-mode dontAsk /usage",ReadReport,cancel,TimeSpan.FromSeconds(12),environment,Failure);
        }
        static object ReadReport(TextReader input,TextWriter output,CancellationToken cancel){
            // Slash-command output is a finite report. Never leave stdin open or send a turn.
            output.Close();var text=new StringBuilder();int value;
            while((value=input.Read())>=0){cancel.ThrowIfCancellationRequested();if(text.Length>=65536)throw new QuotaFailure("Quota unavailable",null,0,"Response too large");text.Append((char)value);}
            string report=text.ToString();var failure=Failure(report);if(failure!=null)throw failure;
            return Parse(report,DateTimeOffset.UtcNow);
        }
        static QuotaFailure Failure(string text){
            text=Regex.Replace(text??"",@"\x1b\[[0-9;?]*[ -/]*[@-~]","");
            // Recognize explicit CLI error forms, not a bare number or a mention of login.
            var codes=Regex.Matches(text,@"(?im)^\s*(?:API Error:\s*|(?:Error:\s*)?(?:Failed to fetch usage(?: data)?:\s*)?Request failed with status code\s+)(401|403|429)\b");
            int status=0;
            foreach(Match match in codes){int code=int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);if(code==429||status!=429&&(code==403||status==0))status=code;}
            if(status==0&&Regex.IsMatch(text,@"(?im)^\s*(?:Error:\s*)?Not logged in(?:\s|[.·]|$)"))status=401;
            if(status==0)return null;
            var retry=Regex.Match(text,@"(?im)^[ \t]*Retry-After:[ \t]*([^\r\n]+)\r?$");
            return new QuotaFailure(status==429?"Refresh rate limited":status==403?"Quota access denied":"Login required",status==429&&retry.Success?QuotaFailure.ParseRetryAfter(retry.Groups[1].Value.Trim(),DateTimeOffset.UtcNow):null,status,"CLI response");
        }
        internal static QuotaReading Parse(string text,DateTimeOffset now){
            var missing=new QuotaReading{Provider="Claude",Source="CLI",Status="Quota unavailable",Observed=now,FailureKind="Invalid response"};
            if(text==null||text.Length>65536)return missing;
            text=Regex.Replace(text,@"\x1b\[[0-9;?]*[ -/]*[@-~]","");
            var blocks=new Dictionary<string,StringBuilder>();StringBuilder selected=null;
            foreach(string raw in text.Split('\n')){
                string line=raw.Trim();string key=null;
                if(Regex.IsMatch(line,@"^Current session(?:\s*:|\s*$)",RegexOptions.IgnoreCase))key="5-hour";
                else if(Regex.IsMatch(line,@"^Current week(?:\s*\(all models\))?(?:\s*:|\s*$)",RegexOptions.IgnoreCase))key="Weekly";
                else if(Regex.IsMatch(line,@"^(?:Current\b|Extra usage\b)",RegexOptions.IgnoreCase))selected=null;
                if(key!=null){if(blocks.ContainsKey(key))return missing;blocks.Add(key,selected=new StringBuilder());}
                if(selected!=null)selected.AppendLine(line);
            }
            var windows=new List<QuotaWindow>();
            foreach(string key in new[]{"5-hour","Weekly"}){
                StringBuilder block;if(!blocks.TryGetValue(key,out block))return missing;
                var percentages=Regex.Matches(block.ToString(),@"(?<![0-9.+\-])([0-9]+(?:\.[0-9]+)?)%\s+(used|left|remaining)\b",RegexOptions.IgnoreCase);
                double amount;if(percentages.Count!=1||!double.TryParse(percentages[0].Groups[1].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out amount)||amount<0||amount>100)return missing;
                windows.Add(new QuotaWindow{Label=key,Remaining=percentages[0].Groups[2].Value.Equals("used",StringComparison.OrdinalIgnoreCase)?100-amount:amount,Reset=Reset(block.ToString(),now,key=="Weekly")});
            }
            return new QuotaReading{Provider="Claude",Source="CLI",Status="Live",Observed=now,Windows=windows,AllWindows=new List<QuotaWindow>(windows)};
        }
        static DateTimeOffset? Reset(string block,DateTimeOffset now,bool weekly){
            var match=Regex.Match(block,@"\bresets\s+(?:(?<month>[A-Za-z]{3})\s+(?<day>[0-9]{1,2}),?\s+(?:at\s+)?)?(?<hour>[0-9]{1,2})(?::(?<minute>[0-9]{2}))?\s*(?<half>am|pm)\s*\((?<zone>[^\r\n()]{1,80})\)",RegexOptions.IgnoreCase);
            if(!match.Success)return null;
            try{
                string zone=match.Groups["zone"].Value;
                // Older Windows Framework does not resolve IANA IDs. Unknown zones remain blank.
                var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"UTC","UTC"},{"Etc/UTC","UTC"},{"Asia/Kuala_Lumpur","Singapore Standard Time"},{"Asia/Singapore","Singapore Standard Time"},{"Asia/Shanghai","China Standard Time"},{"Asia/Tokyo","Tokyo Standard Time"},{"America/New_York","Eastern Standard Time"},{"America/Chicago","Central Standard Time"},{"America/Denver","Mountain Standard Time"},{"America/Los_Angeles","Pacific Standard Time"},{"Europe/London","GMT Standard Time"},{"Europe/Berlin","W. Europe Standard Time"}};
                TimeZoneInfo timezone;try{timezone=TimeZoneInfo.FindSystemTimeZoneById(zone);}catch(TimeZoneNotFoundException){string mapped;if(!aliases.TryGetValue(zone,out mapped))return null;timezone=TimeZoneInfo.FindSystemTimeZoneById(mapped);}
                var local=TimeZoneInfo.ConvertTime(now,timezone);int hour=int.Parse(match.Groups["hour"].Value,CultureInfo.InvariantCulture),minute=match.Groups["minute"].Success?int.Parse(match.Groups["minute"].Value,CultureInfo.InvariantCulture):0;
                if(hour<1||hour>12||minute>59)return null;
                hour=hour%12+(match.Groups["half"].Value.Equals("pm",StringComparison.OrdinalIgnoreCase)?12:0);
                bool dated=match.Groups["month"].Success;int month=local.Month,day=local.Day;
                if(dated){month=DateTime.ParseExact(match.Groups["month"].Value,"MMM",CultureInfo.InvariantCulture).Month;day=int.Parse(match.Groups["day"].Value,CultureInfo.InvariantCulture);}
                var date=new DateTime(local.Year,month,day,hour,minute,0,DateTimeKind.Unspecified);
                if(date<=local.DateTime){if(!dated)date=date.AddDays(1);else if(local.Month==12&&month==1)date=date.AddYears(1);else return null;}
                if(timezone.IsInvalidTime(date)||timezone.IsAmbiguousTime(date))return null;
                var reset=new DateTimeOffset(date,timezone.GetUtcOffset(date));
                return reset>now&&reset-now<=TimeSpan.FromDays(weekly?8:1)?reset:(DateTimeOffset?)null;
            }catch(ArgumentException){return null;}catch(FormatException){return null;}catch(TimeZoneNotFoundException){return null;}catch(InvalidTimeZoneException){return null;}
        }
    }
}
