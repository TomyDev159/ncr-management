using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNumberPlugin_NCR
{
    public class NeshodaAutoNumberPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Získám kontext provádění pluginu
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Spuštím plugin pouze pokud se jedná o vytvoření a cílová entita je con_neshody
            if (context.MessageName.ToLower() != "create" || context.PrimaryEntityName.ToLower() != "con_neshody")
                return;

            // Kontrola, že vstupní data obsahují objekt "Target" a že to je entita
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity newNeshoda)
            {
                // Vytvořím servis pro volání do Dataverse (nutné k dotazům nebo zápisu dat)
                var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                var service = serviceFactory.CreateOrganizationService(context.UserId);

                // Dotaz typu QueryExpression – získá všechny existující záznamy entity "con_neshody"
                var query = new QueryExpression("con_neshody")
                {
                    ColumnSet = new ColumnSet(false) // nechci žádná data, jen počet záznamů
                };

                // Provedu dotaz a uložíme výsledek
                var result = service.RetrieveMultiple(query);

                // Spočítám počet záznamů
                int currentCount = result.Entities.Count;

                // Vygeneruju nové ID přičtením 1 a upravením na formát "NCR-XXXX"
                string generatedId = $"NCR-{(currentCount + 1).ToString("D4")}";

                // Nastavíme hodnotu do pole "con_ID"
                newNeshoda["con_id"] = generatedId;

                
            }
        }
    }
}
