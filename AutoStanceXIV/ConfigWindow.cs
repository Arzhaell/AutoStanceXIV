using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;

namespace AutoStanceXIV;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;

    // Mode avancé : onglet à sélectionner au prochain affichage (ImGui garde ensuite lui-même l'onglet actif).
    private InstanceKind? tabToSelect;

    public ConfigWindow(Plugin plugin)
        : base("AutoStanceXIV###AutoStanceConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
    }

    // À l'ouverture, on se place sur le type d'instance où se trouve le joueur.
    public override void OnOpen() => tabToSelect = plugin.CurrentKind;

    public override void Draw()
    {
        var configuration = plugin.Configuration;

        ImGui.TextUnformatted(Loc.T("Settings:", "Réglages :"));
        ImGui.SameLine();
        if (ImGui.RadioButton(Loc.T("Classic", "Classique"), !configuration.AdvancedMode))
            SetAdvancedMode(false);
        Tooltip(Loc.T("The same settings everywhere.", "Les mêmes réglages partout."));
        ImGui.SameLine();
        if (ImGui.RadioButton(Loc.T("Advanced", "Avancé"), configuration.AdvancedMode))
            SetAdvancedMode(true);
        Tooltip(Loc.T(
            "Separate settings for each type of duty:\nopen world, dungeons, trials, raids, alliance raids.",
            "Des réglages séparés pour chaque type d'instance :\nALÉA, donjon, défis, raid, alliance."));

        ImGui.Separator();
        if (configuration.AdvancedMode)
            DrawKindTabs();
        else
            DrawProfile(configuration.Classic);

        ImGui.Separator();
        if (!configuration.AdvancedMode)
        {
            Checkbox(Loc.T("Only in duties (dungeon, raid, trial…)", "Seulement en instance (donjon, raid, défi…)"),
                     configuration.OnlyInDuty, v => configuration.OnlyInDuty = v);
        }

        Checkbox(Loc.T("Allow in combat", "Autoriser en combat"), configuration.AllowInCombat, v => configuration.AllowInCombat = v);
        Tooltip(Loc.T(
            "The \"On pull\" trigger always acts, since it happens in combat.",
            "Le déclencheur « Au pull » agit toujours, puisqu'il a lieu en combat."));
        if (Checkbox(Loc.T("Show in the server info bar", "Afficher dans la barre d'infos serveur"),
                     configuration.ShowInServerBar, v => configuration.ShowInServerBar = v))
            plugin.UpdateDtrEntry();
        Checkbox(Loc.T("Chat message when the mode changes", "Message dans le chat quand le mode change"),
                 configuration.ChatFeedback, v => configuration.ChatFeedback = v);

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("Language:", "Langue :"));
        ImGui.SameLine();
        if (ImGui.RadioButton("Auto", configuration.Language == PluginLanguage.Auto))
            plugin.SetLanguage(PluginLanguage.Auto);
        Tooltip(Loc.T("Follows the Dalamud language.", "Suit la langue de Dalamud."));
        ImGui.SameLine();
        if (ImGui.RadioButton("English", configuration.Language == PluginLanguage.English))
            plugin.SetLanguage(PluginLanguage.English);
        ImGui.SameLine();
        if (ImGui.RadioButton("Français", configuration.Language == PluginLanguage.French))
            plugin.SetLanguage(PluginLanguage.French);
    }

    // Un onglet par type d'instance ; celui où se trouve le joueur est écrit en vert.
    private void DrawKindTabs()
    {
        ImGui.TextDisabled(Loc.T(
            $"You are currently in: {InstanceKinds.Name(plugin.CurrentKind)}",
            $"Tu es actuellement dans : {InstanceKinds.Name(plugin.CurrentKind)}"));

        if (!ImGui.BeginTabBar("##InstanceKinds"))
            return;

        foreach (var kind in InstanceKinds.All)
        {
            var profile = plugin.Configuration.Advanced[kind];
            var isCurrent = kind == plugin.CurrentKind;
            var flags = tabToSelect == kind ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;

            // « ### » : l'identifiant de l'onglet reste le même quand la langue change.
            if (isCurrent)
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            var open = ImGui.BeginTabItem($"{InstanceKinds.TabLabel(kind)}###{kind}", flags);
            if (isCurrent)
                ImGui.PopStyleColor();

            Tooltip($"{InstanceKinds.Description(kind)}\nStance: {ModeLabel(profile.Mode)}");
            if (!open)
                continue;

            DrawProfile(profile);
            DrawCopyToAll(kind);
            ImGui.EndTabItem();
        }

        tabToSelect = null;
        ImGui.EndTabBar();
    }

    private static string ModeLabel(StanceMode mode) => mode switch
    {
        StanceMode.Enable => "ON",
        StanceMode.Disable => "OFF",
        _ => "pause",
    };

    private void DrawProfile(StanceProfile profile)
    {
        ImGui.TextUnformatted(Loc.T("Tank stance:", "Stance de tank :"));
        if (ImGui.RadioButton(Loc.T("Enable automatically", "Activer automatiquement"), profile.Mode == StanceMode.Enable))
            SetMode(profile, StanceMode.Enable);
        if (ImGui.RadioButton(Loc.T("Remove automatically", "Retirer automatiquement"), profile.Mode == StanceMode.Disable))
            SetMode(profile, StanceMode.Disable);
        if (ImGui.RadioButton(Loc.T("Pause (do nothing)", "Pause (ne rien faire)"), profile.Mode == StanceMode.None))
            SetMode(profile, StanceMode.None);

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("When to apply it:", "Quand l'appliquer :"));
        if (ImGui.RadioButton(Loc.T("Always", "En permanence"), profile.Trigger == TriggerMode.Continuous))
            SetTrigger(profile, TriggerMode.Continuous);
        Tooltip(Loc.T(
            "Puts the stance back in the desired state as soon as it changes,\neven if you change it manually.",
            "Remet la stance dans l'état voulu dès qu'elle en sort,\nmême si tu la changes à la main."));
        if (ImGui.RadioButton(Loc.T("At specific moments:", "À certains moments :"), profile.Trigger == TriggerMode.OnEvents))
            SetTrigger(profile, TriggerMode.OnEvents);
        Tooltip(Loc.T(
            "Applies the desired state once at the checked moments,\nthen lets you change it manually.",
            "Applique l'état voulu une fois aux moments cochés,\npuis te laisse la changer à la main."));

        ImGui.Indent();
        ImGui.BeginDisabled(profile.Trigger != TriggerMode.OnEvents);

        Checkbox(Loc.T("Zone change, job change, resurrection", "Entrée en zone, changement de job, résurrection"),
                 profile.TriggerOnZoneOrJob, v => profile.TriggerOnZoneOrJob = v);

        Checkbox(Loc.T("Countdown", "Compte à rebours"), profile.TriggerOnCountdown, v => profile.TriggerOnCountdown = v);
        Tooltip(Loc.T("So the stance is in place before the first hit.", "Pour que la stance soit en place avant le premier coup."));
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        var seconds = profile.CountdownSeconds;
        if (ImGui.SliderInt("##CountdownSeconds", ref seconds, 1, 30, Loc.T("%d s before the end", "%d s avant la fin")))
            profile.CountdownSeconds = seconds;
        if (ImGui.IsItemDeactivatedAfterEdit())
            plugin.Configuration.Save();

        Checkbox(Loc.T("On pull", "Au pull"), profile.TriggerOnPull, v => profile.TriggerOnPull = v);
        Tooltip(Loc.T("When combat starts, once per fight.", "Au début du combat, une fois par combat."));
        ImGui.SameLine();
        Checkbox(Loc.T("Bosses only", "Boss uniquement"), profile.PullBossOnly, v => profile.PullBossOnly = v);
        Tooltip(Loc.T("Ignores trash packs.", "Ignore les packs d'adds."));

        Checkbox(Loc.T("Duty recommence (after a wipe)", "Reprise de l'instance (après un wipe)"),
                 profile.TriggerOnDutyRecommence, v => profile.TriggerOnDutyRecommence = v);
        Tooltip(Loc.T("When the fight restarts at the starting point after a wipe.", "Quand le combat reprend au point de départ après un wipe."));

        ImGui.EndDisabled();
        ImGui.Unindent();
    }

    // Ctrl doit être maintenu : le bouton écrase les réglages de tous les autres types.
    private void DrawCopyToAll(InstanceKind sourceKind)
    {
        ImGui.Spacing();
        ImGui.BeginDisabled(!ImGui.GetIO().KeyCtrl);
        if (ImGui.Button(Loc.T("Copy these settings to every duty type", "Copier ces réglages vers tous les types d'instance")))
        {
            var source = plugin.Configuration.Advanced[sourceKind];
            foreach (var kind in InstanceKinds.All)
            {
                if (kind != sourceKind)
                    plugin.Configuration.Advanced[kind] = source.Clone();
            }

            plugin.Configuration.Save();
            plugin.OnSettingsChanged();
        }

        ImGui.EndDisabled();
        Tooltip(Loc.T("Hold Ctrl to confirm.", "Maintiens Ctrl pour confirmer."));
    }

    private void SetAdvancedMode(bool advanced)
    {
        if (plugin.Configuration.AdvancedMode == advanced)
            return;

        plugin.Configuration.AdvancedMode = advanced;
        plugin.Configuration.Save();
        tabToSelect = plugin.CurrentKind;
        plugin.OnSettingsChanged();
    }

    private void SetMode(StanceProfile profile, StanceMode mode)
    {
        profile.Mode = mode;
        plugin.Configuration.Save();
        plugin.OnSettingsChanged();
    }

    private void SetTrigger(StanceProfile profile, TriggerMode trigger)
    {
        profile.Trigger = trigger;
        plugin.Configuration.Save();
    }

    private bool Checkbox(string label, bool value, Action<bool> setter)
    {
        if (!ImGui.Checkbox(label, ref value))
            return false;

        setter(value);
        plugin.Configuration.Save();
        return true;
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }
}
