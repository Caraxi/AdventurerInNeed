using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

namespace AdventurerInNeed {
    public class AdventurerInNeed : IDalamudPlugin {
        public string Name => "Adventurer in Need";

        public AdventurerInNeedConfig PluginConfig { get; private set; }

        private bool drawConfigWindow;

        public List<ContentRoulette> RouletteList;

        private delegate IntPtr CfPreferredRoleChangeDelegate(IntPtr data);

        private Hook<CfPreferredRoleChangeDelegate> cfPreferredRoleChangeHook;

        internal PreferredRoleList LastPreferredRoleList;

        private readonly Queue<(string url, NameValueCollection data)> webhookMessageQueue = new Queue<(string url, NameValueCollection data)>();

        private Task webhookTask;
        private CancellationTokenSource webhookCancellationTokenSource;

        [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
        [PluginService] public static DataManager Data { get; private set; } = null!;
        [PluginService] public static ChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;

        public void Dispose() {
            PluginInterface.UiBuilder.Draw -= this.BuildUI;
            cfPreferredRoleChangeHook?.Disable();
            cfPreferredRoleChangeHook?.Dispose();
            webhookCancellationTokenSource?.Cancel();

            while (webhookTask != null && !webhookTask.IsCompleted) {
                Thread.Sleep(1);
            }

            webhookTask?.Dispose();
            webhookCancellationTokenSource?.Dispose();
            RemoveCommands();
        }

        public AdventurerInNeed() {
            this.PluginConfig = (AdventurerInNeedConfig) PluginInterface.GetPluginConfig() ?? new AdventurerInNeedConfig();
            this.PluginConfig.Init(this);

            PluginConfig.Webhooks.RemoveAll(string.IsNullOrEmpty);

            var cfPreferredRolePtr = SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4B 6C");

            if (cfPreferredRolePtr == IntPtr.Zero) {
                PluginLog.LogError("Failed to hook the cfPreferredRoleChange method.");
                return;
            }

#if DEBUG
            drawConfigWindow = true;
#endif

            PluginInterface.UiBuilder.OpenConfigUi += () => {
                this.drawConfigWindow = true;
            };

            RouletteList = Data.GetExcelSheet<ContentRoulette>().ToList();
            cfPreferredRoleChangeHook = new Hook<CfPreferredRoleChangeDelegate>(cfPreferredRolePtr, new CfPreferredRoleChangeDelegate(CfPreferredRoleChangeDetour));
            cfPreferredRoleChangeHook.Enable();
            webhookCancellationTokenSource = new CancellationTokenSource();
            webhookTask = Task.Run(WebhookTaskAction);
            PluginInterface.UiBuilder.Draw += this.BuildUI;

            SetupCommands();
        }

        private void WebhookTaskAction() {
            using var wc = new WebClient();
            
            while (!webhookCancellationTokenSource.IsCancellationRequested) {
                webhookCancellationTokenSource.Token.WaitHandle.WaitOne(1000);
                if (webhookCancellationTokenSource.IsCancellationRequested) {
                    break;
                }

                if (webhookMessageQueue.Count > 0) {
                    var (url, data) = webhookMessageQueue.Dequeue();
                    wc.UploadValues(url, data);
                    webhookCancellationTokenSource.Token.WaitHandle.WaitOne(1000);
                }
            }
        }

        private IntPtr CfPreferredRoleChangeDetour(IntPtr data) {
            UpdatePreferredRoleList(Marshal.PtrToStructure<PreferredRoleList>(data));
            return cfPreferredRoleChangeHook.Original(data);
        }

        private void UpdatePreferredRoleList(PreferredRoleList preferredRoleList) {
#if DEBUG
            PluginLog.Log("Updating Preferred Role List");
#endif
            LastPreferredRoleList ??= preferredRoleList;

            foreach (var roulette in RouletteList.Where(roulette => roulette.ContentRouletteRoleBonus.Row != 0)) {
                try {
                    var rouletteConfig = PluginConfig.Roulettes[roulette.RowId];
                    if (!rouletteConfig.Enabled) continue;

                    var role = preferredRoleList.Get(roulette.ContentRouletteRoleBonus.Row);
                    var oldRole = LastPreferredRoleList.Get(roulette.ContentRouletteRoleBonus.Row);

#if DEBUG
                    PluginLog.Log($"{roulette.Name}: {oldRole} => {role}");

                    if (role != oldRole || PluginConfig.AlwaysShowAlert) {
#else
                    if (role != oldRole) {
#endif
                        ShowAlert(roulette, rouletteConfig, role);
                    }

#if DEBUG
                } catch (Exception ex) {
                    PluginLog.LogError(ex.ToString());
#else
                } catch {
                    // Ignored
#endif
                }
            }

            LastPreferredRoleList = preferredRoleList;
        }

        internal void ShowAlert(ContentRoulette roulette, RouletteConfig config, PreferredRole role) {
            if (!config.Enabled) return;

            var doAlert = role switch {
                PreferredRole.Tank => config.Tank,
                PreferredRole.Healer => config.Healer,
                PreferredRole.DPS => config.DPS,
                _ => false
            };

            if (!doAlert) return;

            if (PluginConfig.InGameAlert) {
                ushort roleForegroundColor = role switch {
                    PreferredRole.Tank => 37,
                    PreferredRole.Healer => 504,
                    PreferredRole.DPS => 545,
                    _ => 0,
                };

                var icon = role switch {
                    PreferredRole.Tank => BitmapFontIcon.Tank,
                    PreferredRole.Healer => BitmapFontIcon.Healer,
                    PreferredRole.DPS => BitmapFontIcon.DPS,
                    _ => BitmapFontIcon.Warning
                };

                var payloads = new Payload[] {
                    new UIForegroundPayload(500),
                    new TextPayload(roulette.Name),
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
                    xivChat.Name = this.Name;
                }

                ChatGui.PrintChat(xivChat);
            }

            if (PluginConfig.WebhookAlert && PluginConfig.Webhooks.Count > 0) {
                var nvc = new NameValueCollection {{"username", Name}, {"content", $"**{roulette.Name}** needs a **{role}**"}};
                foreach (var webhook in PluginConfig.Webhooks) {
                    webhookMessageQueue.Enqueue((webhook, nvc));
                }
            }
        }

        public void SetupCommands() {
            CommandManager.AddHandler("/pbonus", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}",
                ShowInHelp = true
            });
        }

        public void OnConfigCommandHandler(string command, string args) {
            drawConfigWindow = !drawConfigWindow;
        }

        public void RemoveCommands() {
            CommandManager.RemoveHandler("/pbonus");
        }

        private void BuildUI() {
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
        }
    }
}
