using System;
using System.Windows;
using System.Windows.Controls;
namespace HardwarePulse {
    public sealed partial class Shell {
        string jevSessionStatus;
        // IO is fixed to this provider in production; isolated UI tests replace these delegates.
        Func<bool> jevHasSession=delegate{return ProtectedQuotaCredentials.Exists("Jev");};
        Action<string> jevSaveSession=delegate(string value){ProtectedQuotaCredentials.Save("Jev",value);};
        Action jevRemoveSession=delegate{ProtectedQuotaCredentials.Remove("Jev");};
        void SetJevSessionStatus(string key){jevSessionStatus=key;LocalizeJevSessionStatus();}
        void LocalizeJevSessionStatus(){if(jevSessionStatus!=null)Text("JevSessionStatus",language.Label(jevSessionStatus));}
        void ShowJevSessionGuide(){
            Control<Expander>("JevSessionTools").IsExpanded=true;
            Control<Expander>("JevGuide").IsExpanded=true;
            Control<Expander>("JevSessionTools").BringIntoView();
            SetJevSessionStatus("Session required");
        }
        void WireJevSession(){
            var cookie=Control<PasswordBox>("JevCookie");var agent=Control<TextBox>("JevUserAgent");
            agent.Text=JevQuota.DefaultUserAgent();
            SetJevSessionStatus(!isolated&&jevHasSession()?"Encrypted session saved":"Session required");
            Click("JevOpenBrowser",delegate{if(!JevQuota.OpenConsole())SetJevSessionStatus("Chrome or Edge required");});
            Click("JevGuideReturn",delegate{Control<Expander>("JevGuide").IsExpanded=false;cookie.BringIntoView();cookie.Focus();});
            Click("JevSessionSave",delegate{
                string value=cookie.Password;cookie.Clear();
                if(!JevSession.ValidCookie(value)||!JevSession.ValidUserAgent(agent.Text))SetJevSessionStatus("Enter a valid Cookie value. The previous session was not changed.");
                else{
                    try{
                        jevSaveSession(JevSession.Encode(value,agent.Text));quotas.CredentialsChanged("Jev",true);quotaRefreshBefore.Remove("Jev");
                        SetJevSessionStatus("Encrypted session saved. Enable Jev to read the balance.");
                    }catch{SetJevSessionStatus("Could not store the session for this Windows user.");}
                }
                value=null;RenderQuota();UpdateDesktop();QueueSave();
            });
            Click("JevSessionRemove",delegate{
                cookie.Clear();
                try{
                    jevRemoveSession();quotas.CredentialsChanged("Jev",false);quotas.Enable("Jev",false);quotaRefreshBefore.Remove("Jev");
                    Control<CheckBox>("QuotaJev").IsChecked=false;settings.Data["quotaJev"]=false;SetJevSessionStatus("Session required");
                }catch{SetJevSessionStatus("Could not remove the session. The stored session was not changed.");}
                RenderQuota();UpdateDesktop();QueueSave();
            });
        }
    }
}
