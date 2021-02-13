

using Newtonsoft.Json;

namespace MetabaseCLI
{
    public struct SessionCredentials
    {
        [Newtonsoft.Json.JsonIgnore()]
        public string Server { get; set; }

        [Newtonsoft.Json.JsonProperty("username")]
        public string UserName { get; set; }
        [Newtonsoft.Json.JsonProperty("password")]
        public string Password { get; set; }
    }
}