using Dalamud.Configuration;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Utility;

namespace AdventurerInNeed {
    public class RouletteConfig {
        public bool Enabled;
        public bool Tank;
        public bool Healer;
        public bool DPS;
        public bool OnlyIncomplete;
    }

    public class AdventurerInNeedConfig : IPluginConfiguration {
        [NonSerialized]
        private AdventurerInNeed plugin;

        public Dictionary<uint, RouletteConfig> Roulettes { get; set; } = new();

#if DEBUG
        public bool AlwaysShowAlert { get; set; }
#endif

        public int Version { get; set; }
        public bool InGameAlert { get; set; }
        public XivChatType ChatType { get; set; } = XivChatType.SystemMessage;

        public void Init(AdventurerInNeed plugin) {
            this.plugin = plugin;
        }

        public void Save() {
            AdventurerInNeed.PluginInterface.SavePluginConfig(this);
        }

        public bool DrawConfigUI() {
            var drawConfig = true;

            var scale = ImGui.GetIO().FontGlobalScale;

            var modified = false;

            ImGui.SetNextWindowSize(new Vector2(420 * scale, 350), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(420 * scale, 350), new Vector2(640 * scale, 650));
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, ImGuiWindowFlags.NoCollapse);

#if DEBUG
            var alwaysShowAlert = AlwaysShowAlert;
            if (ImGui.Checkbox("Debug: Always Alert", ref alwaysShowAlert)) {
                AlwaysShowAlert = alwaysShowAlert;
                Save();
            }

            if (ImGui.Button("Debug: Print All Alerts")) {
                foreach (var r in plugin.RouletteList) {
                    if (r.ContentRouletteRoleBonus.Row > 0) {
                        try {
                            plugin.ShowAlert(r, Roulettes[r.RowId], PreferredRole.Tank);
                            plugin.ShowAlert(r, Roulettes[r.RowId], PreferredRole.Healer);
                            plugin.ShowAlert(r, Roulettes[r.RowId], PreferredRole.DPS);
                        } catch (Exception ex) {
                            AdventurerInNeed.PluginLog.Error(ex.ToString());
                        }
                    }
                }
            }
#endif

            var inGameAlerts = InGameAlert;
            if (ImGui.Checkbox("Send alerts in game chat.", ref inGameAlerts)) {
                InGameAlert = inGameAlerts;
                Save();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);

            var selectedDetails = ChatType.GetDetails();

            if (ImGui.BeginCombo("###chatType", ChatType == XivChatType.None ? "Any" : (selectedDetails == null ? ChatType.ToString() : selectedDetails.FancyName))) {

                foreach (var chatType in ((XivChatType[]) Enum.GetValues(typeof(XivChatType)))) {

                    var details = chatType.GetDetails();

                    if (ImGui.Selectable(chatType == XivChatType.None ? "Any" : (details == null ? chatType.ToString() : details.FancyName), chatType == ChatType)) {
                        ChatType = chatType;
                        Save();
                    }

                    if (chatType == ChatType) ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            ImGui.Separator();
            ImGui.Columns(7, "###cols", false);
            ImGui.SetColumnWidth(0, 60f * scale);
            ImGui.SetColumnWidth(1, ImGui.GetWindowWidth() - 340f * scale);
            ImGui.SetColumnWidth(2, 40f * scale);
            ImGui.SetColumnWidth(3, 40f * scale);
            ImGui.SetColumnWidth(4, 40f * scale);
            ImGui.SetColumnWidth(5, 80f * scale);
            ImGui.SetColumnWidth(6, 80f * scale);

            ImGui.Text("Alerts");
            ImGui.NextColumn();
            ImGui.Text("Roulette");
            ImGui.NextColumn();
            ImGui.Text("T");
            ImGui.NextColumn();
            ImGui.Text("H");
            ImGui.NextColumn();
            ImGui.Text("D");
            ImGui.NextColumn();
            ImGui.Text("Complete");
            ImGui.NextColumn();
            ImGui.Text("Current");
            ImGui.NextColumn();

            ImGui.Separator();
            ImGui.Separator();

            if (plugin.RouletteList != null) {
                foreach (var r in plugin.RouletteList.Where(r => r != null && r.ContentRouletteRoleBonus != null && r.ContentRouletteRoleBonus.Row > 0)) {
                    var rCfg = Roulettes.ContainsKey(r.RowId) ? Roulettes[r.RowId] : new RouletteConfig();
                    
                    var name = r.Name.ToDalamudString().TextValue
                        .Replace("Duty Roulette: ", "")
                        .Replace("DZufallsinhalt: ", "")
                        .Replace("Mission aléatoire : ", "")
                        .Replace("コンテンツルーレット：", "");
                    
                    modified = ImGui.Checkbox($"###rouletteEnabled{r.RowId}", ref rCfg.Enabled) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Enable alerts for '{name}'.");
                    }
                    
                    ImGui.SameLine();
                    modified = ImGui.Checkbox($"###rouletteIncompleteOnly{r.RowId}", ref rCfg.OnlyIncomplete) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Only show alerts if roulette has not been completed today.");
                    }

                    ImGui.NextColumn();

                    ImGui.Text(name);
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteTankEnabled{r.RowId}", ref rCfg.Tank) || modified;
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteHealerEnabled{r.RowId}", ref rCfg.Healer) || modified;
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteDPSEnabled{r.RowId}", ref rCfg.DPS) || modified;
                    ImGui.NextColumn();

                    ImGui.Text(plugin.IsRouletteComplete(r) ? "Yes" : "No");
                    ImGui.NextColumn();
                    
                    if (plugin.LastPreferredRoleList != null) {
                        var currentRole = plugin.LastPreferredRoleList.Get(r.ContentRouletteRoleBonus.Row);
                        ImGui.Text(currentRole.ToString());
                    }

                    ImGui.NextColumn();

                    Roulettes[r.RowId] = rCfg;
                    ImGui.Separator();
                }
            }
            
            ImGui.Columns(1);

            ImGui.End();

            if (modified) {
                Save();
            }

            return drawConfig;
        }
    }
}
