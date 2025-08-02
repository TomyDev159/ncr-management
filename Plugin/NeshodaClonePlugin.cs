using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CloneNeshody
{
        public class NeshodaClonePlugin : IPlugin
        {
            public void Execute(IServiceProvider serviceProvider)
            {
                var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                var service = factory.CreateOrganizationService(context.UserId);

                // Získá ID Neshody z parametru
                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is EntityReference targetRef))
                    throw new InvalidPluginExecutionException("nebyl nalezen potřebný záznam.");

                var neshodaId = targetRef.Id;

                // Načte původní Neshodu
                var origNeshoda = service.Retrieve("con_neshody", neshodaId, new ColumnSet(true));

                // Připraví klon
                var cloneNeshoda = new Entity("con_neshody");
                foreach (var attr in origNeshoda.Attributes)
                {
                    // Vynechá primární klíč, systémová pole a lookup na vlastní entitu
                    if (attr.Key != "con_neshodyid" && attr.Key != "createdon" && attr.Key != "createdby" && attr.Key != "modifiedon" && attr.Key != "modifiedby")
                        cloneNeshoda[attr.Key] = attr.Value;
                }
                // Nastaví nový název/číslování, případně další pole podle potřeby
                cloneNeshoda["con_nazev"] = "Kopie - " + origNeshoda.GetAttributeValue<string>("con_nazev");

                // Uloží klon
                var newNeshodaId = service.Create(cloneNeshoda);

                // Načte a klonuje opatření
                var opatreni = service.RetrieveMultiple(new QueryExpression("con_napravneopatreni")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("con_neshodaid", ConditionOperator.Equal, neshodaId) }
                    }
                });
                foreach (var opatreniEntity in opatreni.Entities)
                {
                    var cloneOpatreni = new Entity("con_napravneopatreni");
                    foreach (var attr in opatreniEntity.Attributes)
                    {
                        if (attr.Key != "con_napravneopatreniid" && attr.Key != "createdon" && attr.Key != "createdby" && attr.Key != "modifiedon" && attr.Key != "modifiedby")
                            cloneOpatreni[attr.Key] = attr.Value;
                    }
                    // Přepojí opatření na nový záznam
                    cloneOpatreni["con_neshodyid"] = new EntityReference("con_neshody", newNeshodaId);
                    service.Create(cloneOpatreni);
                }
            
            }
        }
    
}
