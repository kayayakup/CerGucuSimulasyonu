using System;
using System.Collections.Generic;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioDefinition> CreateDefault()
    {
        var scenarios = new List<ScenarioDefinition>
        {
            Create("NORMAL", "Normal Isletme", "Tum cer gucu trafo merkezleri devrede.", 90),
        };

        var substations = new[]
        {
            "TM-1 (Darıca Sahil)",
            "TM-2 (Darıca Cumhuriyet)",
            "TM-3 (Gebze Marmaray Gar)",
            "TM-4 (Akse Sapağı)",
            "TM-5 (Mutlukent)",
            "TM-6 (OSB)"
        };

        for (int index = 0; index < substations.Length; index++)
        {
            scenarios.Add(Create(
                $"SINGLE_TM_{index + 1}",
                $"Tek TM arızası: TM{index + 1}",
                $"{substations[index]} devre dışı.",
                150,
                substations[index]));
        }

        var nonAdjacentPairs = new[] { (1, 3), (2, 4), (3, 5), (4, 6) };
        foreach (var (first, second) in nonAdjacentPairs)
        {
            scenarios.Add(Create(
                $"NON_ADJACENT_{first}_{second}",
                $"Komşu olmayan TM arızası: TM{first} + TM{second}",
                "Komşu olmayan iki trafo merkezi devre dışı.",
                210,
                substations[first - 1],
                substations[second - 1]));
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
                substations[first - 1],
                substations[second - 1]));
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
