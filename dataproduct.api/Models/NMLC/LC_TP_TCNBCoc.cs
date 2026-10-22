namespace dataproduct.api.Models.NMLC
{
    // Ngưỡng tiêu chuẩn nội bộ (TCNB) theo (chỉ tiêu, loại cốc)
    public class LC_TP_TCNBCoc
    {
        public int Id { get; set; }

        public int ChiTieuId { get; set; }

        public byte Loai { get; set; }

        public decimal? GiaTriTCNB { get; set; }
    }
}
