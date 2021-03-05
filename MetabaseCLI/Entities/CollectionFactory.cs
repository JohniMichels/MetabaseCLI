
using System.Collections.Generic;

namespace MetabaseCLI.Entities
{
    public class CollectionFactory : EntityFactory
    {
        public CollectionFactory(Session session)
        :base(
            "collection",
            new [] { 
                "id",
                "location",
                "name",
                "personal_owner_id",
                "archived"
            },
            session,
            archivedItems: "archived=true",
            collectionField: "parent_id"
            )
        { }
    }
}