using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace MetabaseCLI
{
    public class DashboardFactory: EntityFactory
    {
        public DashboardFactory(): base(
            "dashboard",
            new []{
                "id",
                "name",
                "description",
                "collection_id",
                "ordered_cards",
                "parameters",
                "archived"
            },
            new Dictionary<string, IEnumerable<string>>() {
                {
                    "ordered_cards",
                    new [] {
                        "card_id",
                        "parameter_mappings",
                        "visualization_settings",
                        "sizeX",
                        "sizeY",
                        "row",
                        "col"
                    }
                }
            }
        ) 
        { 
            AfterCreateCallback = this.AfterCreate;
            AfterUpdateCallback = this.AfterUpdate;
        }

        private static IDictionary<string, dynamic> GenerateCardPutRequestBody(
            IDictionary<string, dynamic> card,
            int dashCardId
        ) => new Dictionary<string, dynamic>()
        {
            {
                "cards",
                new List<IDictionary<string, dynamic>>(){
                    new Dictionary<string, dynamic>(){
                        {"id", dashCardId}
                    }.Merge(card)
                }
            }
        };

        private IObservable<IDictionary<string, dynamic>> CreateDashCards(
            Session session,
            int dashId,
            IEnumerable<IDictionary<string, dynamic>> cards)
        {
            return cards.ToObservable()
                .SelectMany(card => 
                    session
                        .Post<IDictionary<string, dynamic>>(
                            $"dashboard/{dashId}/cards",
                            new Dictionary<string, dynamic>() {
                                {"cardId", card["card_id"]}
                            })
                        .SelectMany(response => 
                            session.Put<IDictionary<string, dynamic>>(
                                $"dashboard/{dashId}/cards",
                                GenerateCardPutRequestBody(card, (int)(response["id"]))))
                );
        }

        private IObservable<IDictionary<string, dynamic>> AfterCreate(
            Session session,
            IDictionary<string, dynamic> beforeCreate,
            IDictionary<string, dynamic> afterCreate
        )
        {
            int dashId = afterCreate["id"];
            if(!beforeCreate.ContainsKey("ordered_cards"))
            {
                return this.Get(session, dashId);
            }
            
            return CreateDashCards(
                session,
                dashId,
                beforeCreate["ordered_cards"].ToObject<IEnumerable<IDictionary<string, dynamic>>>())
                .Concat(this.Get(session, dashId))
                .LastAsync();
        }

        private IObservable<IDictionary<string, dynamic>> AfterUpdate(
            Session session,
            IDictionary<string, dynamic> beforeUpdate,
            IDictionary<string, dynamic> afterUpdate
        )
        {
            var dashId = (int)(afterUpdate["id"]);

            if(!beforeUpdate.ContainsKey("ordered_cards"))
            {
                return this.Get(session, dashId);
            }

            var deletions = this.Get(
                    session,
                    dashId,
                    internalFields: this.InternalFields.Merge(
                        new Dictionary<string, IEnumerable<string>>()
                        {
                            {"ordered_cards", new [] {"id"}}
                        })
                ).Select(dash => (IEnumerable<IDictionary<string, dynamic>>)dash["ordered_cards"])
                .SelectMany(dashCards =>
                    dashCards.ToObservable().SelectMany(dashCard =>
                        session.Delete<IDictionary<string, dynamic>>(
                        $"dashboard/{dashId}/cards?dashcardId={dashCard["id"]}")
                    )
                );
            
            var creations = CreateDashCards(
                session,
                dashId,
                (IEnumerable<IDictionary<string, dynamic>>)(
                    beforeUpdate["ordered_cards"].ToObject<IEnumerable<IDictionary<string, dynamic>>>())
            );
            var updates = (new[] { deletions, creations }).ToObservable().SelectMany(o => o);
            return updates.Concat(this.Get(session, dashId)).LastAsync();
        }
    }
}