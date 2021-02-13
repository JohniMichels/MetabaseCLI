
namespace MetabaseCLI.Entities
{
    public class CardFactory : EntityFactory
    {
        public CardFactory():
        base(
            "card",
            new [] {
                "id",
                "name",
                "description",
                "collection_id",
                "dataset_query",
                "display",
                "visualization_settings",
                "archived"
            }
        ){ }
    }
}