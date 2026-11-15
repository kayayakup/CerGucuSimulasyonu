using CerGucuSimulasyonu.Models;
using CerGucuSimulasyonu.Services;
using Xunit;

namespace CerGucuSimulasyonu.Tests;

public sealed class SimulationBehaviorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void EngineRejectsNonPositiveOrNonFiniteTimeSteps(double timeStepSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RailwaySimulationEngine(new SimulationData()).AdvanceOneStep(timeStepSeconds));
    }

    [Theory]
    [InlineData(1.0, 20.0, 120.0, 0.0)]
    [InlineData(0.0, 20.0, 20.0, 0.0)]
    [InlineData(-1.0, 20.0, 0.0, 80.0)]
    public void RequiredEffortsSplitSignedMotionDemand(
        double accelerationMps2,
        double resistanceKn,
        double expectedTractionKn,
        double expectedBrakingKn)
    {
        var effort = SimulationFormulas.RequiredEffortsKn(
            effectiveMassTon: 100,
            accelerationMps2,
            resistanceKn);

        Assert.Equal(expectedTractionKn, effort.TractionEffortKn, 6);
        Assert.Equal(expectedBrakingKn, effort.BrakingEffortKn, 6);
    }

    [Fact]
    public void ScenarioCatalogIncludesEveryConfiguredSubstation()
    {
        var data = new SimulationData();
        for (int i = 1; i <= 7; i++)
        {
            data.TrafoMerkezleri.Add(new TrafoMerkezi { Ad = $"TM-{i}" });
        }

        IReadOnlyList<ScenarioDefinition> scenarios = ScenarioCatalog.CreateDefault(data);

        Assert.Contains(
            scenarios,
            scenario => scenario.Id == "SINGLE_TM_7" &&
                        scenario.OutOfServiceSubstations.Contains("TM-7"));
    }

    [Fact]
    public async Task ScenarioRunnerUsesScenarioHeadwayAndStopsAtRequestedDuration()
    {
        var data = new SimulationData { HatUzunlugu = 1000 };
        data.Trenler.Add(new TrenKonumu { Id = 1, TrenAdi = "T1" });
        data.Trenler.Add(new TrenKonumu { Id = 2, TrenAdi = "T2" });
        var scenario = new ScenarioDefinition(
            "HEADWAY_TEST",
            "Headway test",
            "Scenario-specific headway",
            0.5,
            new HashSet<string>());

        SimulationRunResult result = Assert.Single(await new ScenarioRunner().RunAsync(
            data,
            new[] { scenario },
            durationSeconds: 1,
            timeStepSeconds: 0.6));

        Assert.Equal(1, result.DurationSeconds);
        Assert.DoesNotContain("SEFER BEKLİYOR", result.Snapshots[^1].Trains[1].State);
    }

    [Fact]
    public void EngineReportsMechanicalBrakingBelowRegenerativeTransitionSpeed()
    {
        var data = new SimulationData { HatUzunlugu = 1000 };
        data.Tren.DavisA = 0;
        data.Tren.DavisB = 0;
        data.Tren.DavisC = 0;
        data.Trenler.Add(new TrenKonumu
        {
            Id = 1,
            Konum = 500,
            HatTipi = "H1",
            Yon = "ileri",
            AnlikHiz = 3,
            IsDispatched = true
        });
        data.Trenler.Add(new TrenKonumu
        {
            Id = 2,
            Konum = 650,
            HatTipi = "H1",
            Yon = "ileri",
            AnlikHiz = 0,
            IsDispatched = true
        });

        new RailwaySimulationEngine(data).AdvanceOneStep(0.05);

        Assert.True(data.Trenler[0].AnlikMekanikFrenKuvvetiKn > 0);
    }

    [Fact]
    public void EngineSuppliesRunningResistanceAtSteadySpeed()
    {
        var data = new SimulationData { HatUzunlugu = 1000 };
        data.Tren.DavisA = 100;
        data.Tren.DavisB = 0;
        data.Tren.DavisC = 0;
        data.TrafoMerkezleri.Add(new TrafoMerkezi
        {
            Ad = "TM-1",
            Direnc = 15,
            FiderKabloDirenci = 2,
            GeriDonusKabloDirenci = 1.5,
            DilasKonumuH1 = 500,
            DilasKonumuH2 = 500
        });
        data.Trenler.Add(new TrenKonumu
        {
            Id = 1,
            Konum = 500,
            HatTipi = "H1",
            Yon = "ileri",
            AnlikHiz = 20,
            HedefHiz = 20,
            IsDispatched = true
        });

        new RailwaySimulationEngine(data).AdvanceOneStep(0.05);

        Assert.True(data.Trenler[0].AnlikCekisKuvvetiKn > 0);
        Assert.True(data.Trenler[0].CekilenGucKw > 0);
        Assert.InRange(data.Trenler[0].AnlikHiz, 19.9, 20.1);
    }

    [Fact]
    public void SectionMetricsCountTrainAtInclusiveStartBoundary()
    {
        var data = new SimulationData { HatUzunlugu = 1000 };
        data.Trenler.Add(new TrenKonumu
        {
            Id = 1,
            Konum = 500,
            HatTipi = "H1",
            Yon = "ileri",
            IsDispatched = true
        });
        data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu
        {
            SeksiyonNo = 1,
            BaslangicKm = 0,
            BitisKm = 500,
            HatTipi = "H1"
        });
        data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu
        {
            SeksiyonNo = 2,
            BaslangicKm = 500,
            BitisKm = 1000,
            HatTipi = "H1"
        });

        new RailwaySimulationEngine(data).AdvanceOneStep(0.05);

        Assert.Equal(0, data.KatenerSeksiyonlari[0].AktifTrenSayisi);
        Assert.Equal(1, data.KatenerSeksiyonlari[1].AktifTrenSayisi);
    }

    [Fact]
    public void ComplianceEvaluatorDoesNotClaimUnverifiedVoltagePass()
    {
        var result = new SimulationRunResult
        {
            Scenario = new ScenarioDefinition(
                "TEST",
                "Test",
                "Test",
                90,
                new HashSet<string>())
        };
        result.Snapshots.Add(new SimulationSnapshot(
            0,
            0,
            0,
            0,
            0,
            0,
            1500,
            1500,
            0,
            0,
            0,
            0,
            Array.Empty<TrainSnapshot>(),
            Array.Empty<SubstationSnapshot>(),
            Array.Empty<SectionSnapshot>()));
        var reference = new StandardReference(
            "EN 50163",
            "Unverified",
            "Unverified",
            "Voltage",
            "Minimum voltage",
            "Unverified",
            "Source edition and time limits are unverified.",
            "MinimumTrainVoltage");

        ComplianceResult evaluation = Assert.Single(
            new ComplianceEvaluator().Evaluate(result, new[] { reference }));

        Assert.Equal(ComplianceStatus.Review, evaluation.Status);
    }
}
