using System;
using System.Collections.Generic;
using System.Linq;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioDefinition> CreateDefault(SimulationData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        TrafoMerkezi[] substations = data.TrafoMerkezleri.ToArray();
        var scenarios = new List<ScenarioDefinition>
        {
            Create("NORMAL", "Normal Isletme", "Tum cer gucu trafo merkezleri devrede.", 90),
        };

        for (int index = 0; index < substations.Length; index++)
        {
            scenarios.Add(Create(
                $"SINGLE_TM_{index + 1}",
                $"Tek TM arızası: TM{index + 1}",
                $"{substations[index].Ad} devre dışı.",
                150,
                substations[index].Ad));
        }

        for (int firstIndex = 0; firstIndex + 2 < substations.Length; firstIndex++)
        {
            int secondIndex = firstIndex + 2;
            int first = firstIndex + 1;
            int second = secondIndex + 1;
            scenarios.Add(Create(
                $"NON_ADJACENT_{first}_{second}",
                $"Komşu olmayan TM arızası: TM{first} + TM{second}",
                "Komşu olmayan iki trafo merkezi devre dışı.",
                210,
                substations[firstIndex].Ad,
                substations[secondIndex].Ad));
        }

        for (int index = 0; index < substations.Length - 1; index++)
        {
            int first = index + 1;
            int second = index + 2;
            scenarios.Add(Create(
                $"ADJACENT_{first}_{second}",
                $"Komşu TM arızası: TM{first} + TM{second}",
                "Komşu iki trafo merkezi devre dışı.",
                300,
                substations[index].Ad,
                substations[index + 1].Ad));
        }

        return scenarios;
    }

    private static ScenarioDefinition Create(
        string id,
        string name,
        string description,
        double headwaySeconds,
        params string[] outOfService)
    {
        return new ScenarioDefinition(
            id,
            name,
            description,
            headwaySeconds,
            new HashSet<string>(outOfService, StringComparer.OrdinalIgnoreCase));
    }
}
