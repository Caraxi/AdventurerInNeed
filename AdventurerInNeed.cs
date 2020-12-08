using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Plugin;

namespace AdventurerInNeed {
    public class AdventurerInNeed : IDalamudPlugin {
        public string Name => "Adventurer in Need";
        public DalamudPluginInterface PluginInterface { get; private set; }
        public AdventurerInNeedConfig PluginConfig { get; private set; }

        private bool drawConfigWindow;

        public List<ContentRoulette> RouletteList;

        private delegate IntPtr CfPreferredRoleChangeDelegate(IntPtr data);

        private Hook<CfPreferredRoleChangeDelegate> cfPreferredRoleChangeHook;

        internal PreferredRoleList LastPreferredRoleList;

        private readonly Queue<(string url, NameValueCollection data)> webhookMessageQueue = new Queue<(string url, NameValueCollection data)>();

        private Task webhookTask;
        private CancellationTokenSource webhookCancellationTokenSource;

        public void Dispose() {
            PluginInterface.UiBuilder.OnBuildUi -= this.BuildUI;
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

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.PluginInterface = pluginInterface;
            this.PluginConfig = (AdventurerInNeedConfig) pluginInterface.GetPluginConfig() ?? new AdventurerInNeedConfig();
            this.PluginConfig.Init(this, pluginInterface);

            PluginConfig.Webhooks.RemoveAll(string.IsNullOrEmpty);

            var cfPreferredRolePtr = PluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4B 6C");

            if (cfPreferredRolePtr == IntPtr.Zero) {
                PluginLog.LogError("Failed to hook the cfPreferredRoleChange method.");
                return;
            }

#if DEBUG
            drawConfigWindow = true;
#endif

            PluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => {
                this.drawConfigWindow = true;
            };

            RouletteList = pluginInterface.Data.GetExcelSheet<ContentRoulette>().ToList();
            cfPreferredRoleChangeHook = new Hook<CfPreferredRoleChangeDelegate>(cfPreferredRolePtr, new CfPreferredRoleChangeDelegate(CfPreferredRoleChangeDetour));
            cfPreferredRoleChangeHook.Enable();
            webhookCancellationTokenSource = new CancellationTokenSource();
            webhookTask = Task.Run(WebhookTaskAction);
            PluginInterface.UiBuilder.OnBuildUi += this.BuildUI;

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

                var payloads = new Payload[] {
                    new UIForegroundPayload(PluginInterface.Data, 500),
                    new TextPayload(roulette.Name),
                    new UIForegroundPayload(PluginInterface.Data, 0),
                    new TextPayload(" needs a "),
                    new IconPayload((uint) (82 + role)),
                    new UIForegroundPayload(PluginInterface.Data, roleForegroundColor),
                    new TextPayload(role.ToString()),
                    new UIForegroundPayload(PluginInterface.Data, 0),
                    new TextPayload("."),
                };

                var sestring = new SeString(payloads);

                var xivChat = new XivChatEntry() {
                    MessageBytes = sestring.Encode()
                };

                if (PluginConfig.ChatType != XivChatType.None) {
                    xivChat.Type = PluginConfig.ChatType;
                    xivChat.Name = this.Name;
                }

                PluginInterface.Framework.Gui.Chat.PrintChat(xivChat);
            }

            if (PluginConfig.WebhookAlert && PluginConfig.Webhooks.Count > 0) {
                var nvc = new NameValueCollection {{"username", Name}, {"content", $"**{roulette.Name}** needs a **{role}**"}};
                foreach (var webhook in PluginConfig.Webhooks) {
                    webhookMessageQueue.Enqueue((webhook, nvc));
                }
            }
        }

        public void SetupCommands() {
            PluginInterface.CommandManager.AddHandler("/pbonus", new Dalamud.Game.Command.CommandInfo(OnConfigCommandHandler) {
                HelpMessage = $"Open config window for {this.Name}",
                ShowInHelp = true
            });
        }

        public void OnConfigCommandHandler(string command, string args) {
            drawConfigWindow = !drawConfigWindow;
        }

        public void RemoveCommands() {
            PluginInterface.CommandManager.RemoveHandler("/pbonus");
        }

        private void BuildUI() {
            drawConfigWindow = drawConfigWindow && PluginConfig.DrawConfigUI();
        }
    }
}
