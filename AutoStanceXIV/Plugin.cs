using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AutoStanceXIV;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/autostance";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDtrBar DtrBar { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("AutoStanceXIV");
    private readonly ConfigWindow configWindow;
    private readonly StanceController controller;
    private readonly IDtrBarEntry dtrEntry;

    public Configuration Configuration { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();
        Loc.Update(Configuration.Language);
        controller = new StanceController(Configuration);

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        PluginInterface.LanguageChanged += OnDalamudLanguageChanged;

        RegisterCommand();

        // Barre d'infos serveur : clic gauche = on/off, clic droit = pause.
        dtrEntry = DtrBar.Get("AutoStanceXIV");
        dtrEntry.OnClick = e =>
        {
            if (e.ClickType == MouseClickType.Right)
                SetMode(StanceMode.None);
            else
                ToggleMode();
        };
        UpdateDtrEntry();
    }

    public void Dispose()
    {
        dtrEntry.Remove();
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        controller.Dispose();
    }

    public void SetMode(StanceMode mode)
    {
        Configuration.Mode = mode;
        Configuration.Save();
        controller.RequestApply();
        UpdateDtrEntry();

        if (Configuration.ChatFeedback)
            ChatGui.Print(Loc.T($"Stance: {Describe(mode)}", $"Stance : {Describe(mode)}"), "AutoStanceXIV");
    }

    public void ToggleMode() => SetMode(Configuration.Mode == StanceMode.Enable ? StanceMode.Disable : StanceMode.Enable);

    public void SetLanguage(PluginLanguage language)
    {
        Configuration.Language = language;
        Configuration.Save();
        RefreshLanguage();
    }

    public void UpdateDtrEntry()
    {
        dtrEntry.Shown = Configuration.ShowInServerBar;
        dtrEntry.Text = Configuration.Mode switch
        {
            StanceMode.Enable => "Stance: ON",
            StanceMode.Disable => "Stance: OFF",
            _ => "Stance: —",
        };
        dtrEntry.Tooltip = Loc.T(
            $"AutoStanceXIV: {Describe(Configuration.Mode)}\nLeft click: on/off — Right click: pause",
            $"AutoStanceXIV : {Describe(Configuration.Mode)}\nClic gauche : on/off — Clic droit : pause");
    }

    public static string Describe(StanceMode mode) => mode switch
    {
        StanceMode.Enable => Loc.T("enabled automatically", "activée automatiquement"),
        StanceMode.Disable => Loc.T("removed automatically", "retirée automatiquement"),
        _ => Loc.T("paused (the plugin leaves it alone)", "en pause (le plugin n'y touche pas)"),
    };

    private void OnDalamudLanguageChanged(string languageCode)
    {
        if (Configuration.Language == PluginLanguage.Auto)
            RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        Loc.Update(Configuration.Language);
        CommandManager.RemoveHandler(CommandName);
        RegisterCommand();
        UpdateDtrEntry();
    }

    private void RegisterCommand()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = Loc.T(
                "Opens the settings.\n"
                + "/autostance on → keep the stance on\n"
                + "/autostance off → keep the stance off\n"
                + "/autostance toggle → switch between on and off\n"
                + "/autostance pause → stop touching the stance",
                "Ouvre la configuration.\n"
                + "/autostance on → garder la stance activée\n"
                + "/autostance off → garder la stance retirée\n"
                + "/autostance toggle → basculer entre on et off\n"
                + "/autostance pause → ne plus toucher à la stance"),
        });
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "":
                configWindow.Toggle();
                break;
            case "on":
            case "activer":
                SetMode(StanceMode.Enable);
                break;
            case "off":
            case "retirer":
                SetMode(StanceMode.Disable);
                break;
            case "toggle":
                ToggleMode();
                break;
            case "pause":
            case "none":
                SetMode(StanceMode.None);
                break;
            default:
                ChatGui.PrintError(
                    Loc.T($"Unknown argument \"{args}\". Use on, off, toggle or pause.",
                          $"Argument inconnu « {args} ». Utilise on, off, toggle ou pause."),
                    "AutoStanceXIV");
                break;
        }
    }
}
