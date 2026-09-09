namespace dataproduct.api.Models;

/// <summary>
/// Lưu giá trị IDSlab đã sửa tay qua popup "Sửa slab thủ công" — tách khỏi HRC1_Slab.IDSlab
/// để cột đó luôn bất biến, mirror đúng SLAB_ID từ TSC. UpsertFromApiAsync (Sync) chỉ đối chiếu
/// được đúng nếu HRC1_Slab.IDSlab không đổi; nếu Edit ghi đè thẳng lên đó, lần Sync sau TSC vẫn
/// trả về SLAB_ID gốc, không match được record đã sửa nữa và sẽ insert nhầm một dòng mới.
///
/// Đọc dữ liệu (Search/GetSlabsByPhieu/Export) coi IDSlab hiệu lực = Hrc1SlabEdit.IDSlab (nếu
/// có record) ?? HRC1_Slab.IDSlab. Xóa mềm/khôi phục (HRC1_Slab.IsDeleted) không liên quan tới
/// bảng này — 2 cơ chế độc lập, xem thảo luận trong lịch sử sửa Hrc1SlabRepository.
/// </summary>
public class Hrc1SlabEdit
{
    public int Id { get; set; }
    public int IdSlab { get; set; }        // FK → Hrc1Slab.Id, 1-1 (unique)
    public string IDSlab { get; set; } = "";
    public DateTime NgayCapNhat { get; set; }
}
