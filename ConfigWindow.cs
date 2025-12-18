using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AdventurerInNeed;

public class ConfigWindow(AdventurerInNeed plugin, IDalamudPluginInterface pluginInterface) : Window($"{plugin.Name} Config", ImGuiWindowFlags.NoCollapse) {

    private AdventurerInNeedConfig Config => plugin.PluginConfig;
    
    public override void OnOpen() {
        Size = new Vector2(420, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints() {
            MinimumSize = new Vector2(420, 350),
            MaximumSize = new Vector2(640, 650)
        };
    }

    public override void Draw() {
            
            var scale = ImGui.GetIO().FontGlobalScale;

            var modified = false;



#if DEBUG
            if (ImGui.Button("Debug: Print All Alerts")) {
                foreach (var r in plugin.RouletteList) {
                    if (r.ContentRouletteRoleBonus.RowId > 0) {
                        try {
                            plugin.ShowAlert(r, Config.Roulettes[r.RowId], ContentsRouletteRole.Tank);
                            plugin.ShowAlert(r, Config.Roulettes[r.RowId], ContentsRouletteRole.Healer);
                            plugin.ShowAlert(r, Config.Roulettes[r.RowId], ContentsRouletteRole.Dps);
                        } catch (Exception ex) {
                            AdventurerInNeed.PluginLog.Error(ex.ToString());
                        }
                    }
                }
            }
#endif

            var inGameAlerts = Config.InGameAlert;
            if (ImGui.Checkbox("Send alerts in game chat.", ref inGameAlerts)) {
                Config.InGameAlert = inGameAlerts;
                modified = true;
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);

            var selectedDetails = Config.ChatType.GetDetails();

            if (ImGui.BeginCombo("###chatType", Config.ChatType == XivChatType.None ? "Any" : (selectedDetails == null ? Config.ChatType.ToString() : selectedDetails.FancyName))) {

                foreach (var chatType in ((XivChatType[]) Enum.GetValues(typeof(XivChatType)))) {

                    var details = chatType.GetDetails();

                    if (ImGui.Selectable(chatType == XivChatType.None ? "Any" : (details == null ? chatType.ToString() : details.FancyName), chatType == Config.ChatType)) {
                        Config.ChatType = chatType;
                        modified = true;
                    }

                    if (chatType == Config.ChatType) ImGui.SetItemDefaultFocus();
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
                
                foreach (var r in plugin.RouletteList.Where(r => r.RowId != 0 && r.ContentRouletteRoleBonus.RowId != 0)) {
                    var rCfg = Config.Roulettes.TryGetValue(r.RowId, out var value) ? value : new RouletteConfig();
                    
                    var name = r.Name.ToDalamudString().TextValue
                        .Replace("Duty Roulette: ", "")
                        .Replace("DZufallsinhalt: ", "")
                        .Replace("Mission aléatoire : ", "")
                        .Replace("コンテンツルーレット：", "");
                    
                    modified = ImGui.Checkbox($"###rouletteEnabled{r.RowId}", ref rCfg.Enabled) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Enable alerts for '{name}'.");
                    }


                    var e = rCfg.Enabled && (rCfg.Tank || rCfg.Healer || rCfg.DPS) && (Config.InGameAlert);
                    
                    using var textColor = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey3, !e);
                    using var checkMarkColor = ImRaii.PushColor(ImGuiCol.CheckMark, ImGuiColors.DalamudGrey3, !e);

                    ImGui.NextColumn();

                    ImGui.Text(name);
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteTankEnabled{r.RowId}", ref rCfg.Tank) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Show alerts when roulette changes to tank bonus.");
                    }
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteHealerEnabled{r.RowId}", ref rCfg.Healer) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Show alerts when roulette changes to healer bonus.");
                    }
                    ImGui.NextColumn();
                    modified = ImGui.Checkbox($"###rouletteDPSEnabled{r.RowId}", ref rCfg.DPS) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Show alerts when roulette changes to DPS bonus.");
                    }
                    ImGui.NextColumn();

                   
                    
                    modified = ImGui.Checkbox($"###rouletteIncompleteOnly{r.RowId}", ref rCfg.OnlyIncomplete) || modified;
                    if (ImGui.IsItemHovered()) {
                        ImGui.SetTooltip($"Only show alerts if roulette has not been completed today.");
                    }
                    
                    ImGui.SameLine();
                    
                    ImGui.Text(plugin.IsRouletteComplete(r) ? "Yes" : "No");
                    
                    ImGui.NextColumn();
                    
                    if (plugin.LastPreferredRoleList != null) {
                        var currentRole = plugin.LastPreferredRoleList[r.ContentRouletteRoleBonus.RowId];
                        ImGui.Text(currentRole.ToString());
                    }

                    ImGui.NextColumn();

                    Config.Roulettes[r.RowId] = rCfg;
                    ImGui.Separator();
                }
            }
            
            ImGui.Columns(1);

            if (modified) {
                pluginInterface.SavePluginConfig(Config);
            }
        }
}
