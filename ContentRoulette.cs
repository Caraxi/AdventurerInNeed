using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;

namespace AdventurerInNeed {
    [Sheet("ContentRoulette")]
    public class ContentRoulette : IExcelRow {
        public SeString Name;
        public LazyRow<ContentRouletteRoleBonus> ContentRouletteRoleBonus;

        public uint RowId { get; set; }
        public uint SubRowId { get; set; }

        public void PopulateData(RowParser parser, Lumina.Lumina lumina, Language language) {
            RowId = parser.Row;
            SubRowId = parser.SubRow;

            Name = parser.ReadColumn<SeString>(1);
            ContentRouletteRoleBonus = new LazyRow<ContentRouletteRoleBonus>(lumina, parser.ReadColumn<byte>(16), language);
        }
    }
}
