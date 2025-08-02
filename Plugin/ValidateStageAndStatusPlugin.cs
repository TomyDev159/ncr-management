// Plugin kontroluje, zda má neshoda správně nastavený stav při přechodu mezi fázemi BPF (Business Process Flow).
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

public class ValidateStageAndStatusPlugin : IPlugin
{
    
    public void Execute(IServiceProvider serviceProvider)
    {
        // Získání kontextu vykonávání – obsahuje info o probíhající operaci (např. jaký záznam, jaká entita, jaká operace atd.)
        var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
        var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        var service = serviceFactory.CreateOrganizationService(context.UserId);
        // Služba pro logování
        var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

        // Spouštím jen, když probíhá "Update"
        if (context.MessageName != "Update" || context.PrimaryEntityName != "con_ncrprocess")
        {   
            return;
        }

        // Ověřujeme, že vstupní parametry obsahují "Target" a že se mění "activestageid"
        if (!(context.InputParameters["Target"] is Entity target) || !target.Attributes.Contains("activestageid"))
        {
            return;
        }

        // Získáme ID fáze, do které uživatel přechází
        var toId = target.GetAttributeValue<EntityReference>("activestageid")?.Id;

        // Získáme ID původní fáze, odkud se odchází – PreImage (obraz původních hodnot před změnou)
        if (!context.PreEntityImages.Contains("PreImage"))
        {
            return;
        }

        var preImage = context.PreEntityImages["PreImage"];
        // ID původní fáze
        var fromId = preImage.GetAttributeValue<EntityReference>("activestageid")?.Id;
        // Reference na navázanou neshodu – potřebujeme z ní vyčíst stav
        var neshodaRef = preImage.GetAttributeValue<EntityReference>("bpf_con_neshodyid");

        // Ověření, že máme všechny potřebné hodnoty
        if (fromId == null || toId == null || neshodaRef == null)
        {
            return;
        }

        // Načteme název fází (pomocná metoda GetStageName)
        var from = GetStageName(service, fromId.Value);
        var to = GetStageName(service, toId.Value);

        tracing.Trace($"Z fáze: {from}");
        tracing.Trace($"Do fáze: {to}");

        // Povolené přechody mezi fázemi a jakou hodnotu stavu neshody očekáváme pro daný přechod
        var povolene = new Dictionary<(string, string), int>
        {
            { ("Nahlášeno", "V Řešení"), 769140001 },
            { ("Uzavřeno", "V Řešení"), 769140001 },
            { ("V Řešení", "Nahlášeno"), 769140000 },
            { ("V Řešení", "Uzavřeno"), 769140002 },
        };

        // Zjistíme, jestli přechod, který právě probíhá, je v povolených a případně zjistíme očekávanou hodnotu stavu neshody
        if (!povolene.TryGetValue((from, to), out int expected))
        {
            tracing.Trace("Tento přechod není validovaný – plugin končí.");
            return;
        }

        // Načteme navázanou neshodu a zjistíme její aktuální stav
        var neshoda = service.Retrieve("con_neshody", neshodaRef.Id, new ColumnSet("con_stav"));
        var stav = neshoda.GetAttributeValue<OptionSetValue>("con_stav")?.Value;

        tracing.Trace($"Očekávaný stav: {expected}");
        tracing.Trace($"Skutečný stav: {stav}");

        // Pokud aktuální stav neshody NEODPOVÍDÁ očekávanému stavu pro tento přechod, zabráníme přechodu a vyhodíme chybu
        if (stav != expected)
        {
            tracing.Trace("Stav neodpovídá – házím chybu");
            throw new InvalidPluginExecutionException($"Při přechodu z fáze '{from}' do '{to}' musí být stav '{PopisStavu(expected)}'.");
        }

        tracing.Trace("Vše OK – plugin dokončen");
    }

    // Pomocná metoda pro načtení názvu fáze podle jejího ID
    private string GetStageName(IOrganizationService service, Guid stageId)
    {
        var q = new QueryExpression("processstage")
        {
            ColumnSet = new ColumnSet("stagename"),
            Criteria = { Conditions = { new ConditionExpression("processstageid", ConditionOperator.Equal, stageId) } }
        };
        var result = service.RetrieveMultiple(q);
        // Pokud najdeme záznam, vrátíme název fáze
        return result.Entities.Count > 0 ? result.Entities[0].GetAttributeValue<string>("stagename") : null;
    }

    // Pomocná metoda na převod číselné hodnoty stavu na text
    private string PopisStavu(int val)
    {
        if (val == 769140000) return "Nahlášeno";
        if (val == 769140001) return "V Řešení";
        if (val == 769140002) return "Uzavřeno";
        return "";
    }
}
