using System.Collections.Generic;


using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace MetabaseCLI
{
    public static class SessionExtensions
    {
        
        private static IObservable<TResponse> InternalSend<TResponse>(
            this Session session,
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
                    response.EnsureSuccessStatusCode();
                    return Observable.FromAsync(() => response.Content.ReadAsStringAsync());
                }).Select(response => JsonConvert.DeserializeObject<TResponse>(response));
        }

        private static IObservable<TResponse> InternalSend<TResponse>(
            this Session session,
            IObservable<HttpResponseMessage> generator
        )
        where TResponse : notnull
        {
            return session.InternalSend<TResponse>("", content => generator);
        }

        private static readonly object padLock = new object();
        public static IObservable<string> InvalidateSession(
            this Session session
        )
        {
            return session.Client.DefaultRequestHeaders.Contains("X-Metabase-Session") ?
                Observable.Return(session.Client.DefaultRequestHeaders.GetValues("X-Metabase-Session").First()) :
                session.InternalSend<IDictionary<string, string>>(
                    session.Credentials,
                    c => Observable.FromAsync(() => session.Client.PostAsync("session", c))
                ).Select(d => d["id"]
                ).Do(id =>
                {
                    lock (padLock)
                    {
                        if (!session.Client.DefaultRequestHeaders.Contains("X-Metabase-Session"))
                            session.Client.DefaultRequestHeaders.Add("X-Metabase-Session", id);
                    }
                });
        }

        private static IObservable<TResponse> InvalidatedSend<TResponse>(
            this Session session,
            object objectBody,
            Func<HttpContent, IObservable<HttpResponseMessage>> generator
        )
        where TResponse : notnull
        {
            var invalidator = session.InvalidateSession();
            var requester = session.InternalSend<TResponse>(
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
        where TResponse : notnull => session.InvalidatedSend<TResponse>(
                objectBody,
                content => Observable.FromAsync(() =>session.Client.PostAsync(path, content))
            );

        public static IObservable<TResponse> Get<TResponse>(
            this Session session,
            string path
        )
        where TResponse : notnull => session.InvalidatedSend<TResponse>(
                Observable.FromAsync(() => session.Client.GetAsync(path))
        );
        
        public static IObservable<TResponse> Put<TResponse>(
            this Session session,
            string path,
            object objectBody
        )
        where TResponse : notnull => session.InvalidatedSend<TResponse>(
                objectBody,
                content => Observable.FromAsync(() => session.Client.PutAsync(path, content))
            );

        public static IObservable<TResponse> Delete<TResponse>(
            this Session session,
            string path
        )
        where TResponse : notnull
        {
            var invalidator = session.InvalidateSession();
            var requester = Observable.FromAsync(() => session.Client.DeleteAsync(path));
            return invalidator
                .Select<string, (string Auth, object Response)>(i => (i, new object()))
                .Concat(requester.Select(r => (Auth: "", Response: (object)r)))
                .Skip(2)
                .Select(r => r.Response)
                .Cast<TResponse>();
        }
    }
} 