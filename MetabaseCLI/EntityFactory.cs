

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reactive.Linq;

namespace MetabaseCLI
{
    public abstract class EntityFactory : ICommandBuilder
    {
        public delegate IObservable<IDictionary<string, dynamic?>> EntityUpsertedCallback(
            Session session,
            IDictionary<string, dynamic?> beforeUpsert,
            IDictionary<string, dynamic?> afterUpsert
        );

        public string Name { get; private set; }
        public IEnumerable<string> Fields { get; private set; }
        public IDictionary<string, IEnumerable<string>> InternalFields { get; private set; }
        public EntityUpsertedCallback? AfterCreateCallback { get; protected set; }
        public EntityUpsertedCallback? AfterUpdateCallback { get; protected set; }
        public string ArchivedItems { get; private set; }
        public Session Session { get; set; }

        public string CollectionField { get; private set; }

        public EntityFactory(
            string name,
            IEnumerable<string> fields,
            Session session,
            IDictionary<string, IEnumerable<string>>? internalFields = null,
            EntityUpsertedCallback? afterCreateCallback = null,
            EntityUpsertedCallback? afterUpdateCallback = null,
            string archivedItems = "f=archived",
            string collectionField = "collection_id"
        )
        {
            Name = name;
            Fields = fields;
            InternalFields = internalFields ?? new Dictionary<string, IEnumerable<string>>();
            AfterCreateCallback = afterCreateCallback;
            AfterUpdateCallback = afterUpdateCallback;
            ArchivedItems = archivedItems;
            CollectionField = collectionField;
            Session = session;
        }

        private static IObservable<IDictionary<string, dynamic?>> ParseGet(
            IObservable<IDictionary<string, dynamic?>> source,
            IEnumerable<string> fields,
            IDictionary<string, IEnumerable<string>> internalFields
        )
        {
            return source
                .Select(item => item.FilterKeys(fields).ToDictionary(
                    kv => kv.Key,
                    kv => 
                        (
                            internalFields.ContainsKey(kv.Key)
                            && kv.Value is IEnumerable<dynamic> valueArray
                        ) ?
                        valueArray
                            .Select(internalItem =>
                            ((IDictionary<string, dynamic?>)(internalItem
                                .ToObject<IDictionary<string, dynamic?>>()))
                                .FilterKeys(internalFields[kv.Key])
                        ) :
                        kv.Value
                    )
                ).Cast<IDictionary<string, dynamic?>>();
        }

        public IObservable<IDictionary<string, dynamic?>> Get(
            int id,
            IEnumerable<string>? fields = null,
            IDictionary<string, IEnumerable<string>>? internalFields = null
        ) => ParseGet(
            Session.Get<IDictionary<string, dynamic?>>($"{Name}/{id}"),
            fields ?? Fields,
            internalFields ?? InternalFields);


        public IObservable<IDictionary<string, dynamic?>> Get(
            IEnumerable<string>? fields = null,
            IDictionary<string, IEnumerable<string>>? internalFields = null,
            bool includeArchivedItems = true
        ) => ParseGet(
                (includeArchivedItems ?
                    Session.Get<IEnumerable<IDictionary<string, dynamic?>>>($"{Name}")
                    .Merge(Session.Get<IEnumerable<IDictionary<string, dynamic?>>>($"{Name}?{ArchivedItems}")) :
                    Session.Get<IEnumerable<IDictionary<string, dynamic?>>>($"{Name}"))
                    .SelectMany(r => r.ToObservable())
                    .Distinct(entity => entity["id"]),
                fields ?? this.Fields,
                internalFields ?? this.InternalFields
            );
        

        public IObservable<IDictionary<string, dynamic?>> Create(
            IDictionary<string, dynamic?> entity
        ) => Session.Post<IDictionary<string, dynamic?>>(
            Name,
            entity
        ).SelectMany(
            created => AfterCreateCallback?.Invoke(Session, entity, created) ??
                Observable.Return(created)
        );

        public IObservable<IDictionary<string, dynamic?>> Update(
            IDictionary<string, dynamic?> entity,
            int id
        ) => Session.Put<IDictionary<string, dynamic?>>(
            $"{Name}/{id}",
            entity
        ).SelectMany(
            updated =>
                AfterUpdateCallback?.Invoke(Session, entity, updated) ??
                Observable.Return(updated)
        );

    public IObservable<IDictionary<string, dynamic?>> Archive(
            int id
        ) => Session.Put<IDictionary<string, dynamic?>>(
            $"{Name}/{id}",
            new Dictionary<string, bool>() {
                {"archived", true}
            });

        public IObservable<IDictionary<string, dynamic?>> Delete(
            int id
        ) => Session
            .Delete<IDictionary<string, dynamic?>>($"{Name}/{id}");

        public IDictionary<string, dynamic?> AtPosition(
            IDictionary<string, dynamic?> entity,
            int? collectionId
        )
        {
            entity[CollectionField] = collectionId;
            return entity;
        }

        public virtual Command Build()
        {
            return this.GenerateCommand();
        }
    }
}
