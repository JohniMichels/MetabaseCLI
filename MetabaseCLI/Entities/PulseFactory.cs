
using System.Collections.Generic;

namespace MetabaseCLI.Entities
{
    public class PulseFactory : EntityFactory
    {
        public PulseFactory(Session session)
        :base(
            "pulse",
            new [] { 
                "id",
                "collection_id",
                "name",
                "cards",
                "channels",
                "skip_if_empty",
                "archived"
            },
            session,
            new Dictionary<string, IEnumerable<string>>(){
                {
                    "cards",
                    new [] { "id", "include_csv", "include_xls" }
                },
                {
                    "channels",
                    new [] { 
                        "id", "schedule_type", "schedule_hour",
                        "schedule_day", "channel_type",
                        "schedule_frame", "enabled"
                    }
                }
            })
        { }
    }
}