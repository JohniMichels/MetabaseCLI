using System.Collections.Generic;


using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MetabaseCLI
{
    public static class SessionExtensions
    {
        
        private static IObservable<TResponse> InternalSend<TResponse>(
            object objectBody,
            Func<HttpContent, IObservable<HttpResponseMessage>> generator
        )
        {
            var request = generator(
                new StringContent(
                    JsonConvert.SerializeObject(objectBody),
                    Encoding.UTF8,
                    "application/json"
                ));
            return request
                .SelectMany(response =>
                {
                    return Observable.FromAsync(() => response.Content.ReadAsStringAsync());
                }).Select(response => JsonConvert.DeserializeObject<TResponse>(response));
        }

        private static readonly object padLock = new();
        public static IObservable<string> InvalidateSession(
            this Session session
        )
        {
            lock (padLock)
            {
                session.Logger.LogTrace(
                    "Checking if session is authenticated for {Server}",
                    session.SessionCredentials.Server
                );
                if (session.Client.DefaultRequestHeaders.Contains("X-Metabase-Session"))
                {
                    session.Logger.LogTrace(
                        "Session is already authenticated for {Server} with {SessionId}",
                        session.SessionCredentials.Server,
                        session.Client.DefaultRequestHeaders.GetValues("X-Metabase-Session").First()
                    );
                    return Observable.Return(
                        session
                            .Client
                            .DefaultRequestHeaders
                            .GetValues("X-Metabase-Session")
                            .First()
                    );
                }
                else
                {
                    session.Logger.LogTrace(
                        "Session is not authenticated for {Server}",
                        session.SessionCredentials.Server
                    );
                    session.Logger.LogDebug(
                        "Requesting new authentication token for {Server}", 
                        session.SessionCredentials.Server);
                    return InternalSend<IDictionary<string, string>>(
                        session.SessionCredentials,
                        c => Observable.FromAsync(() =>
                            session.Logger.LogWebRequest(
                                "POST",
                                session.SessionCredentials.Server,
                                "session",
                                session.SessionCredentials,
                                session.Client.PostAsync("session", c)))
                            .Do(r => r.EnsureSuccessStatusCode())
                        ).Select(d => d["id"]
                        ).Catch<string, Exception>(
                            ex =>
                            {
                                session.Logger.LogError(
                                    ex, 
                                    "Authentication failed for {Server}",
                                    session.SessionCredentials.Server);
                                throw ex;
                            }
                        ).Do(
                            id => session.Logger.LogInformation("Successfully authenticated for {Server} with {SessionId}",
                                session.SessionCredentials.Server,
                                id)
                        ).Do(
                            id => session.Client.DefaultRequestHeaders.Add("X-Metabase-Session", id)
                        );
                }
            }
        }

        private static IObservable<TResponse> InvalidatedSend<TResponse>(
            this Session session,
            object objectBody,
            Func<HttpContent, IObservable<HttpResponseMessage>> generator
        )
        where TResponse : notnull
        {
            var invalidator = session.InvalidateSession();
            var requester = InternalSend<TResponse>(
                objectBody,                     
                content => generator(content)
            );                                  
            return invalidator.Select<string, (string Auth, object Response)>(i => (i, new object()))
                .Concat(requester.Select(i => (Auth: "", Response: (object)i)))
                .Select(i => i.Response).Skip(1).Cast<TResponse>();
        }

        private static IObservable<TResponse> InvalidatedSend<TResponse>(
            this Session session,
            IObservable<HttpResponseMessage> generator
        )
        where TResponse : notnull
        => session.InvalidatedSend<TResponse>(
                "", content => generator
            );

        public static IObservable<TResponse> Post<TResponse>(
            this Session session,
            string path,
            object objectBody
        )
        where TResponse : notnull => 
        session.InvalidatedSend<TResponse>(
            objectBody,
            content => Observable.FromAsync(() =>
                session.Logger.LogWebRequest(
                    "POST",
                    session.SessionCredentials.Server,
                    path,
                    objectBody,
                    session.Client.PostAsync(path, content))));
        
        public static IObservable<TResponse> Get<TResponse>(
            this Session session,
            string path
        )
        where TResponse : notnull => 
        session.InvalidatedSend<TResponse>(
            Observable.FromAsync(() =>
                session.Logger.LogWebRequest(
                    "GET",
                    session.SessionCredentials.Server,
                    path,
                    "",
                    session.Client.GetAsync(path))));
        
        public static IObservable<TResponse> Put<TResponse>(
            this Session session,
            string path,
            object objectBody
        )
        where TResponse : notnull => 
        session.InvalidatedSend<TResponse>(
            objectBody,
            content => Observable.FromAsync(() =>
                session.Logger.LogWebRequest(
                    "PUT",
                    session.SessionCredentials.Server,
                    path,
                    objectBody,
                    session.Client.PutAsync(path, content))));

        public static IObservable<TResponse> Delete<TResponse>(
            this Session session,
            string path
        )
        where TResponse : notnull
        {
            var invalidator = session.InvalidateSession();
            var requester = Observable.FromAsync(() => 
                session.Logger.LogWebRequest(
                    "DELETE",
                    session.SessionCredentials.Server,
                    path,
                    "",
                    session.Client.DeleteAsync(path)));
            return invalidator
                .Select<string, (string Auth, object Response)>(i => (i, new object()))
                .Concat(requester.Select(r => (Auth: "", Response: (object)r)))
                .Skip(2)
                .Select(r => r.Response)
                .Cast<TResponse>();
        }
    }
}