using AutoStanceXIV;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace AutoStanceXIV.Tests;

/// <summary>
/// Conversion des réglages enregistrés par les anciennes versions du plugin.
/// Les fichiers sont lus et écrits avec les mêmes options que Dalamud.
/// </summary>
public class ConfigurationMigrationTests
{
    private static readonly JsonSerializerSettings ReadSettings = new() { TypeNameHandling = TypeNameHandling.Objects };

    private static readonly JsonSerializerSettings WriteSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
    };

    private static Configuration Read(string json) =>
        (Configuration)JsonConvert.DeserializeObject<IPluginConfiguration>(json, ReadSettings)!;

    private static string Write(Configuration configuration) =>
        JsonConvert.SerializeObject(configuration, Formatting.Indented, WriteSettings);

    [Fact]
    public void Version1_SettingsMoveToClassicAndAdvanced()
    {
        // Format 1.0.x : un seul jeu de réglages, à la racine.
        var configuration = Read("""
            {
              "$type": "AutoStanceXIV.Configuration, AutoStanceXIV",
              "Version": 1, "Mode": 2, "Trigger": 1,
              "TriggerOnZoneOrJob": false, "TriggerOnPull": true, "PullBossOnly": false,
              "TriggerOnCountdown": true, "CountdownSeconds": 7, "TriggerOnDutyRecommence": true,
              "Language": 2, "OnlyInDuty": false, "AllowInCombat": false, "ShowInServerBar": false, "ChatFeedback": true
            }
            """);

        Assert.True(configuration.Migrate());

        var classic = configuration.Classic;
        Assert.Equal(StanceMode.Disable, classic.Mode);
        Assert.Equal(TriggerMode.OnEvents, classic.Trigger);
        Assert.False(classic.TriggerOnZoneChange);
        Assert.False(classic.TriggerOnResurrection);
        Assert.False(classic.TriggerOnJobChange);
        Assert.True(classic.TriggerOnPull);
        Assert.False(classic.PullBossOnly);
        Assert.True(classic.TriggerOnCountdown);
        Assert.Equal(7, classic.CountdownSeconds);
        Assert.True(classic.TriggerOnDutyRecommence);

        Assert.Equal(PluginLanguage.French, configuration.Language);
        Assert.False(configuration.AllowInCombat);
        Assert.False(configuration.ShowInServerBar);
        Assert.False(configuration.AdvancedMode);
        Assert.Equal(3, configuration.Version);

        // Chaque onglet avancé part d'une copie des réglages classiques.
        Assert.Equal(InstanceKinds.All.Length, configuration.Advanced.Count);
        foreach (var profile in configuration.Advanced.Values)
        {
            Assert.NotSame(classic, profile);
            Assert.Equal(StanceMode.Disable, profile.Mode);
            Assert.True(profile.TriggerOnDutyRecommence);
        }
    }

    [Fact]
    public void Version0_ExclusivePullModeBecomesPullTrigger()
    {
        // Format 1.0.0 d'origine : « Au pull » était un troisième mode (Trigger = 2).
        var configuration = Read("""
            {
              "$type": "AutoStanceXIV.Configuration, AutoStanceXIV",
              "Version": 0, "Mode": 1, "Trigger": 2, "PullBossOnly": false, "OnlyInDuty": true
            }
            """);

        Assert.True(configuration.Migrate());

        var classic = configuration.Classic;
        Assert.Equal(StanceMode.Enable, classic.Mode);
        Assert.Equal(TriggerMode.OnEvents, classic.Trigger);
        Assert.True(classic.TriggerOnPull);
        Assert.False(classic.PullBossOnly);
        Assert.False(classic.TriggerOnZoneChange);
        Assert.False(classic.TriggerOnResurrection);
        Assert.False(classic.TriggerOnJobChange);

        // « Seulement en instance » : l'onglet ALÉA ne touche pas à la stance.
        Assert.Equal(StanceMode.None, configuration.Advanced[InstanceKind.OpenWorld].Mode);
        Assert.Equal(StanceMode.Enable, configuration.Advanced[InstanceKind.Dungeon].Mode);
    }

    [Fact]
    public void Version2_SingleZoneJobResurrectionCaseIsSplitPerProfile()
    {
        // Format 1.1.0 : une seule case « entrée en zone, changement de job, résurrection » par profil.
        var configuration = Read("""
            {
              "$type": "AutoStanceXIV.Configuration, AutoStanceXIV",
              "Version": 2, "AdvancedMode": true,
              "Classic": { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 1, "Trigger": 1, "TriggerOnZoneOrJob": false },
              "Advanced": {
                "OpenWorld": { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 0, "TriggerOnZoneOrJob": false },
                "Dungeon":   { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 1, "Trigger": 1, "TriggerOnZoneOrJob": true },
                "Trial":     { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 2, "TriggerOnZoneOrJob": false },
                "Raid":      { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 1, "TriggerOnZoneOrJob": true },
                "Alliance":  { "$type": "AutoStanceXIV.StanceProfile, AutoStanceXIV", "Mode": 2, "TriggerOnZoneOrJob": false }
              }
            }
            """);

        Assert.True(configuration.Migrate());
        Assert.True(configuration.AdvancedMode);
        Assert.Equal(3, configuration.Version);

        AssertZoneResurrectionJob(configuration.Classic, false);
        AssertZoneResurrectionJob(configuration.Advanced[InstanceKind.OpenWorld], false);
        AssertZoneResurrectionJob(configuration.Advanced[InstanceKind.Dungeon], true);
        AssertZoneResurrectionJob(configuration.Advanced[InstanceKind.Trial], false);
        AssertZoneResurrectionJob(configuration.Advanced[InstanceKind.Raid], true);
        AssertZoneResurrectionJob(configuration.Advanced[InstanceKind.Alliance], false);
        Assert.Equal(StanceMode.Disable, configuration.Advanced[InstanceKind.Alliance].Mode);
    }

    [Fact]
    public void CurrentFormat_RoundTripKeepsEverythingAndDropsOldKeys()
    {
        var configuration = new Configuration();
        configuration.Migrate();
        configuration.AdvancedMode = true;
        var raid = configuration.Advanced[InstanceKind.Raid];
        raid.Mode = StanceMode.Enable;
        raid.Trigger = TriggerMode.OnEvents;
        raid.TriggerOnZoneChange = true;
        raid.TriggerOnResurrection = false;
        raid.TriggerOnJobChange = true;
        raid.TriggerOnCountdown = true;
        raid.CountdownSeconds = 3;

        var json = Write(configuration);
        Assert.DoesNotContain(StanceProfile.LegacyZoneOrJobKey, json);

        var reloaded = Read(json);
        Assert.False(reloaded.Migrate());

        var reloadedRaid = reloaded.Advanced[InstanceKind.Raid];
        Assert.True(reloaded.AdvancedMode);
        Assert.Equal(StanceMode.Enable, reloadedRaid.Mode);
        Assert.Equal(TriggerMode.OnEvents, reloadedRaid.Trigger);
        Assert.True(reloadedRaid.TriggerOnZoneChange);
        Assert.False(reloadedRaid.TriggerOnResurrection);
        Assert.True(reloadedRaid.TriggerOnJobChange);
        Assert.True(reloadedRaid.TriggerOnCountdown);
        Assert.Equal(3, reloadedRaid.CountdownSeconds);
    }

    [Fact]
    public void NewConfiguration_HasEveryAdvancedTab()
    {
        var configuration = new Configuration();

        Assert.True(configuration.Migrate());
        Assert.Equal(InstanceKinds.All.Length, configuration.Advanced.Count);
        AssertZoneResurrectionJob(configuration.Classic, true);
        Assert.False(configuration.Migrate());
    }

    private static void AssertZoneResurrectionJob(StanceProfile profile, bool expected)
    {
        Assert.Equal(expected, profile.TriggerOnZoneChange);
        Assert.Equal(expected, profile.TriggerOnResurrection);
        Assert.Equal(expected, profile.TriggerOnJobChange);
    }
}
