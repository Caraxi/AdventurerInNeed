using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Timers;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ContentRoulette = Lumina.Excel.Sheets.ContentRoulette;
using InstanceContent = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent;

namespace AdventurerInNeed {
    public class AdventurerInNeed : IDalamudPlugin {
        public string Name => "Adventurer in Need";
        private readonly ConfigWindow _configWindow;
        private readonly WindowSystem _windowSystem = new(nameof(AdventurerInNeed));

        public AdventurerInNeedConfig PluginConfig { get; private set; }

        public readonly List<ContentRoulette> RouletteList;

        internal ContentsRouletteRole[]? LastPreferredRoleList;
        
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;


        private IClientState _clientState;
        private IFramework _framework;

        private CancellationTokenSource _pluginLifespan = new CancellationTokenSource();

        public void Dispose() {
            _pluginLifespan.Cancel();
            RemoveCommands();
        }

        public AdventurerInNeed(IDalamudPluginInterface pluginInterface, IDataManager data, IClientState clientState, IFramework framework) {
            _clientState = clientState;
            _framework = framework;
            
            PluginConfig = pluginInterface.GetPluginConfig() as AdventurerInNeedConfig ?? new AdventurerInNeedConfig();
            
            _configWindow = new ConfigWindow(this, pluginInterface);
            _windowSystem.AddWindow(_configWindow);

            pluginInterface.UiBuilder.OpenConfigUi += () => {
                _configWindow.Toggle();
            };

            RouletteList = data.GetExcelSheet<ContentRoulette>().ToList();

            pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            
            framework.RunOnTick(UpdatePreferredRoleList);
            
            SetupCommands();
        }

        private unsafe void UpdatePreferredRoleList() {
            if (_pluginLifespan.IsCancellationRequested) return;
            _framework.RunOnTick(UpdatePreferredRoleList, TimeSpan.FromSeconds(5), cancellationToken: _pluginLifespan.Token);
            if (!_clientState.IsLoggedIn) return;     
            
#if DEBUG
            PluginLog.Debug("Updating Preferred Role List");
#endif
            var agent = AgentContentsFinder.Instance();
            var preferredRoleList = agent->ContentRouletteRoleBonuses.ToArray();
            LastPreferredRoleList ??= preferredRoleList;

            foreach (var roulette in RouletteList.Where(roulette => roulette.ContentRouletteRoleBonus.RowId != 0)) {
                try {
                    var rouletteConfig = PluginConfig.Roulettes[roulette.RowId];
                    if (!rouletteConfig.Enabled) continue;
                    var role = preferredRoleList[roulette.ContentRouletteRoleBonus.RowId];
                    var oldRole = LastPreferredRoleList[roulette.ContentRouletteRoleBonus.RowId];
#if DEBUG
                    PluginLog.Verbose($"{roulette.Name}: {oldRole} => {role}");
#endif
                    if (role != oldRole) {
                        ShowAlert(roulette, rouletteConfig, role);
                    }
                } catch (Exception ex) {
                    PluginLog.Error(ex.ToString());
                }
            }

            LastPreferredRoleList = preferredRoleList;
        }

        internal void ShowAlert(ContentRoulette roulette, RouletteConfig config, ContentsRouletteRole role) {
            if (!config.Enabled) return;
            if (config.OnlyIncomplete && IsRouletteComplete(roulette)) return;

            var doAlert = role switch {
                ContentsRouletteRole.Tank => config.Tank,
                ContentsRouletteRole.Healer => config.Healer,
                ContentsRouletteRole.Dps => config.DPS,
                _ => false
            };

            if (!doAlert) return;

            if (PluginConfig.InGameAlert) {
                ushort roleForegroundColor = role switch {
                    ContentsRouletteRole.Tank => 37,
                    ContentsRouletteRole.Healer => 504,
                    ContentsRouletteRole.Dps => 545,
                    _ => 0,
                };

                var icon = role switch {
                    ContentsRouletteRole.Tank => BitmapFontIcon.Tank,
                    ContentsRouletteRole.Healer => BitmapFontIcon.Healer,
                    ContentsRouletteRole.Dps => BitmapFontIcon.DPS,
                    _ => BitmapFontIcon.Warning
                };

                var payloads = new Payload[] {
                    new UIForegroundPayload(500),
                    new TextPayload(roulette.Name.ExtractText()),
                    new UIForegroundPayload(0),
                    new TextPayload(" needs a "),
                    new IconPayload(icon),
                    new UIForegroundPayload(roleForegroundColor),
                    new TextPayload(role.ToString()),
                    new UIForegroundPayload(0),
                    new TextPayload("."),
                };

                var seString = new SeString(payloads);

                var xivChat = new XivChatEntry() {
                    Message = seString
                };

                if (PluginConfig.ChatType != XivChatType.None) {
                    xivChat.Type = PluginConfig.ChatType;
                    xivChat.Name = Name;
                }

                ChatGui.Print(xivChat);
            }
        }

        public void SetupCommands() {
            CommandManager.AddHandler("/pbonus", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {Name}",
                ShowInHelp = true
            });
        }

        public void OnConfigCommandHandler(string command, string args) {
            _configWindow.Toggle();
        }

        public void RemoveCommands() {
            CommandManager.RemoveHandler("/pbonus");
        }
        

        public unsafe bool IsRouletteComplete(ContentRoulette roulette) {
            if (roulette.RowId > byte.MaxValue) return false;
            var rouletteController = InstanceContent.Instance();
            return rouletteController->IsRouletteComplete((byte)roulette.RowId);
        }
    }
}
