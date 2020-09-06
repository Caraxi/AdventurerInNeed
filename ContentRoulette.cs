using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace AdventurerInNeed {
    [Sheet("ContentRoulette")]
    public class ContentRoulette : IExcelRow {
        public string Name;
        public LazyRow<ContentRouletteRoleBonus> ContentRouletteRoleBonus;

        public uint RowId { get; set; }
        public uint SubRowId { get; set; }

        public void PopulateData(RowParser parser, Lumina.Lumina lumina, Language language) {
            RowId = parser.Row;
            SubRowId = parser.SubRow;

            Name = parser.ReadColumn<string>(0);
            ContentRouletteRoleBonus = new LazyRow<ContentRouletteRoleBonus>(lumina, parser.ReadColumn<byte>(15), language);
        }
    }
}
