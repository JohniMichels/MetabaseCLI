

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace MetabaseCLI
{
    public class EntityFactory
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

        public string CollectionField { get; private set; }

        public EntityFactory(
            string name,
            IEnumerable<string> fields,
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
            Session session,
            int id,
            IEnumerable<string>? fields = null,
            IDictionary<string, IEnumerable<string>>? internalFields = null
        ) => ParseGet(
            session.Get<IDictionary<string, dynamic?>>($"{Name}/{id}"),
            fields ?? Fields,
            internalFields ?? InternalFields);


        public IObservable<IDictionary<string, dynamic?>> Get(
            Session session,
            IEnumerable<string>? fields = null,
            IDictionary<string, IEnumerable<string>>? internalFields = null
        ) => ParseGet(
                session.Get<IEnumerable<IDictionary<string, dynamic?>>>($"{Name}")
                    .Merge(session.Get<IEnumerable<IDictionary<string, dynamic?>>>($"{Name}?{ArchivedItems}"))
                    .SelectMany(r => r.ToObservable())
                    .Distinct(entity => entity["id"]),
                fields ?? this.Fields,
                internalFields ?? this.InternalFields
            );

        public IObservable<IDictionary<string, dynamic?>> Create(
            Session session,
            IDictionary<string, dynamic?> entity
        ) => session.Post<IDictionary<string, dynamic?>>(
            Name,
            entity
        ).SelectMany(
            created => AfterCreateCallback?.Invoke(session, entity, created) ??
                Observable.Return(created)
        );

        public IObservable<IDictionary<string, dynamic?>> Update(
            Session session,
            IDictionary<string, dynamic?> entity,
            int id
        ) => session.Put<IDictionary<string, dynamic?>>(
            $"{Name}/{id}",
            entity
        ).SelectMany(
            updated =>
                AfterUpdateCallback?.Invoke(session, entity, updated) ??
                Observable.Return(updated)
        );

    public IObservable<IDictionary<string, dynamic?>> Archive(
            Session session,
            int id
        ) => session.Put<IDictionary<string, dynamic?>>(
            $"{Name}/{id}",
            new Dictionary<string, bool>() {
                {"archived", true}
            });

        public IObservable<IDictionary<string, dynamic?>> Delete(
            Session session,
            int id
        ) => session
            .Delete<IDictionary<string, dynamic?>>($"{Name}/{id}");

        public IDictionary<string, dynamic?> AtPosition(
            IDictionary<string, dynamic?> entity,
            int? collectionId
        )
        {
            entity[CollectionField] = collectionId;
            return entity;
        }
    }
}
