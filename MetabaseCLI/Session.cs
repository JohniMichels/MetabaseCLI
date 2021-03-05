using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace MetabaseCLI
{
    public class Session
    {
        internal HttpClient Client { get; set; } = new HttpClient();
        public ILogger Logger { get; private set; }
        public SessionCredentials SessionCredentials { get; set; }

        public Session(ILogger<Session> logger, SessionCredentials sessionCredentials)
        {
            Logger = logger;
            SessionCredentials = sessionCredentials;
            SessionCredentials.PropertyChanged += (sender, e) => RedefineCredentials();
        }

        public SessionCredentials GetCredentials()
        {
            return SessionCredentials;
        }

        private void RedefineCredentials()
        {
            var credentials = GetCredentials();
            Client.BaseAddress = new Uri(
                string.Format("{0}/api/", credentials.Server.TrimEnd('/')));
            Client.DefaultRequestHeaders.Remove("X-Metabase-Session");
        }
    }
}
