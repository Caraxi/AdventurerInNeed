using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AdventurerInNeed {
    public class RouletteConfig {
        public bool Enabled;
        public bool Tank;
        public bool Healer;
        public bool DPS;
        public bool OnlyIncomplete;
    }

    public class AdventurerInNeedConfig : IPluginConfiguration {
        public Dictionary<uint, RouletteConfig> Roulettes { get; set; } = new();
        public int Version { get; set; }
        public bool InGameAlert { get; set; }
        public bool ToastAlert { get; set; } = false;
        public bool WindowsAlert { get; set; } = false;
        public XivChatType ChatType { get; set; } = XivChatType.SystemMessage;
    }
}
