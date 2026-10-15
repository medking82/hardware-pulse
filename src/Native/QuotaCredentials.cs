using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HardwarePulse {
    public sealed partial class Shell {
        readonly System.Collections.Generic.Dictionary<TextBlock,string> quotaCredentialStates=new System.Collections.Generic.Dictionary<TextBlock,string>();
        readonly System.Collections.Generic.Dictionary<DependencyObject,string> quotaCredentialNames=new System.Collections.Generic.Dictionary<DependencyObject,string>();
        void CredentialStatus(TextBlock control,string key){quotaCredentialStates[control]=key;control.Text=language.T(key);}
        void CredentialName(DependencyObject control,string key){quotaCredentialNames[control]=key;System.Windows.Automation.AutomationProperties.SetName(control,language.T(key));}
        void LocalizeQuotaCredentials(){foreach(var state in quotaCredentialStates)state.Key.Text=language.T(state.Value);foreach(var name in quotaCredentialNames)System.Windows.Automation.AutomationProperties.SetName(name.Key,language.T(name.Value));}
        void BuildQuotaCredentials(){
            foreach(var provider in QuotaCatalog.Entries)if(provider.Authentication==QuotaAuthentication.ApiKey)AddQuotaCredential(provider.Id);
        }
        void AddQuotaCredential(string provider){
            var body=new StackPanel();
            var input=new PasswordBox{Name=provider+"ApiKey",MaxLength=16384,Margin=new Thickness(0,8,0,8),MinHeight=32};
            CredentialName(input,provider+" API Key");Window.RegisterName(input.Name,input);
            var status=new TextBlock{Name=provider+"CredentialStatus",TextWrapping=TextWrapping.Wrap,Opacity=.8};Window.RegisterName(status.Name,status);
            var actions=new WrapPanel();
            var save=new Button{Name="Save"+provider+"Key",Content="Save encrypted",Margin=new Thickness(0,0,8,8),Padding=new Thickness(12,6,12,6)};
            var clear=new Button{Name="Clear"+provider+"Key",Content="Remove key",Margin=new Thickness(0,0,0,8),Padding=new Thickness(12,6,12,6)};
            Window.RegisterName(save.Name,save);Window.RegisterName(clear.Name,clear);actions.Children.Add(save);actions.Children.Add(clear);
            body.Children.Add(new TextBlock{Text=provider+" API Key",FontWeight=FontWeights.SemiBold});body.Children.Add(input);body.Children.Add(actions);body.Children.Add(status);
            body.Children.Add(new TextBlock{Text="Stored with Windows user encryption. Saving a key does not enable monitoring.",TextWrapping=TextWrapping.Wrap,Opacity=.8,Margin=new Thickness(0,8,0,0)});
            body.Children.Add(new Expander{Header="Key privacy and permissions",Margin=new Thickness(0,8,0,0),Content=new TextBlock{Text="Encrypted for this Windows user. Sent only to the selected provider for quota queries. Not included in settings, diagnostics or sync. The key may also allow paid API calls; encryption is not protection against malware running as you.",TextWrapping=TextWrapping.Wrap,Opacity=.8}});

            Control<StackPanel>("QuotaCredentialOptions").Children.Add(body);Catalog(body);
            CredentialStatus(status,quotaCredentials.Exists(provider)?"Encrypted key saved; check the quota card for connection status.":"API key required");
            save.Click+=async delegate{
                string value=input.Password;input.Clear();
                if(!ProtectedQuotaCredentials.Valid(value)){CredentialStatus(status,"Enter a valid API key. The previous key was not changed.");return;}
                save.IsEnabled=clear.IsEnabled=input.IsEnabled=false;CredentialStatus(status,"Saving encrypted key…");
                try{await Task.Run(()=>quotaCredentials.Save(provider,value));if(disposed)return;ResetQuotaSource(provider);CredentialStatus(status,"Encrypted key saved; check the quota card for connection status.");}
                catch{if(!disposed)CredentialStatus(status,"Could not save encrypted key");}
                finally{if(!disposed)save.IsEnabled=clear.IsEnabled=input.IsEnabled=true;}
            };
            clear.Click+=delegate{try{quotaCredentials.Clear(provider);input.Clear();ResetQuotaSource(provider);CredentialStatus(status,"API key removed");}catch{CredentialStatus(status,"Could not remove key");}};
        }
        void ResetQuotaSource(string provider){bool on=settings.Flag("quota"+provider,false);quotas.Enable(provider,false);quotas.Enable(provider,on);if(!isolated)quotas.Tick(DateTimeOffset.UtcNow);quotaSignature=null;RenderQuota();UpdateDesktop();}
    }
}
