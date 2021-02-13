using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Reactive.Linq;
using System.Text.Json;

namespace MetabaseCLI
{
    public class Session
    {

        internal HttpClient Client { get; set; } = new HttpClient();
        internal SessionCredentials Credentials { get; set; }

        public Session(SessionCredentials credentials)
        {
            Client = new HttpClient();
            Client.BaseAddress = new Uri(
                string.Format("{0}/api/", credentials.Server.TrimEnd('/'))
            );
            Credentials = credentials;
        }

        public Session(string server, string user, string password) : this(
            new SessionCredentials()
            {
                Server = server,
                UserName = user,
                Password = password
            }
        ){ }
    }
}
