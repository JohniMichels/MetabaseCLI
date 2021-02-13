
using System.Collections.Generic;

namespace MetabaseCLI.Entities
{
    public class CollectionFactory : EntityFactory
    {
        public CollectionFactory()
        :base(
            "collection",
            new [] { 
                "id",
                "location",
                "name",
                "personal_owner_id",
                "archived"
            },
            archivedItems: "archived=true",
            collectionField: "parent_id"
            )
        { }
    }
}