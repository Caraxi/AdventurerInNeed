using System.Linq;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AdventurerInNeed {
    internal enum PreferredRole : byte {
        Unknown = 0,
        Tank = 1,
        Healer = 2,
        DPS = 3,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class PreferredRoleList {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        private readonly byte[] contentRouletteRoleBonus = {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

        public PreferredRole Get(uint index) {
            if (index >= contentRouletteRoleBonus.Length) return PreferredRole.Unknown;

            return contentRouletteRoleBonus[index] switch {
                1 => PreferredRole.Tank,
                2 => PreferredRole.DPS,
                3 => PreferredRole.DPS,
                4 => PreferredRole.Healer,
                _ => PreferredRole.Unknown
            };
        }
    }
}
