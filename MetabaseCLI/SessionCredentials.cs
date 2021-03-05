

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace MetabaseCLI
{
    public class SessionCredentials: INotifyPropertyChanged
    {
        private string server = "";
        [JsonIgnore()]
        public string Server
        {
            get => server;
            set
            {
                server = value;
                NotifyPropertyChanged();
            }
        }

        private string userName = "";
        [JsonProperty("username")]
        public string UserName { 
            get => userName;
            set
            {
                userName = value;
                NotifyPropertyChanged();
            } 
        }

        private string password = "";
        [JsonProperty("password")]
        public string Password {
            get => password;
            set
            {
                password = value;
                NotifyPropertyChanged();
            } 
        }

        public string this[string key]
        {
            get => GetType().GetProperty(key)?.GetValue(this)?.ToString() ?? "";
            set => GetType().GetProperty(key)?.SetValue(this, value);
        }

        public SessionCredentials() {}

        private void NotifyPropertyChanged([CallerMemberName] string? property = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}