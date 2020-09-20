using System.IO;
using Dalamud.Game.Chat.SeStringHandling;

namespace AdventurerInNeed {
    class IconPayload : Payload {

        public byte IconIndex { get; private set; }

        public IconPayload(byte iconIndex) {
            IconIndex = iconIndex;
        }

        protected override byte[] EncodeImpl() {
            byte[] byteList = {2, 18, 2, IconIndex, 3} ;
            return byteList;
        }

        protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
            reader.ReadBytes(3);
            IconIndex = reader.ReadByte();
        }

        public override PayloadType Type { get; } = PayloadType.Unknown;
    }
}
