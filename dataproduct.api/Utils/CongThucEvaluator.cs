using NCalc;
using NCalc.Handlers;

namespace dataproduct.api.Utils
{
    // Phân giải công thức dạng Excel (IF/Round/Sqrt/Pow/so sánh...) cho danh mục LC_SiloTon
    // (tab "Quản lý Silo Tồn") — dùng NCalc. Biến duy nhất trong mọi công thức là "H" (đại
    // diện giá trị nhập: chiều cao hoặc bánh cốc, tuỳ TenNhapLieu của silo đó).
    //
    // QUAN TRỌNG: Excel ROUND luôn làm tròn ra xa số 0 ("round half away from zero"), khác
    // mặc định của NCalc/Math.Round (banker's rounding, round-half-to-even) — đã verify bằng
    // probe thật (Round(2.5,0) mặc định ra 2, bật RoundAwayFromZero mới ra 3, khớp Excel).
    // LUÔN bật ExpressionOptions.RoundAwayFromZero khi tạo Expression để khớp đúng số liệu
    // gốc trong workbook.
    //
    // GHI CHÚ (đã verify bằng probe thật với NCalcSync 7.2.0 — bản NCalc hiện dùng ở đây,
    // API khác hẳn NCalc cũ nên các ví dụ/README cũ trên mạng không áp dụng được):
    // 1) Hàm built-in (Round, if, Sqrt, Pow...) case-SENSITIVE theo đúng tên NCalc định nghĩa
    //    (ví dụ "if" chứ không phải "If"/"IF") — công thức Excel paste nguyên "ROUND(...IF(..."
    //    sẽ ném NCalcFunctionNotFoundException nếu không bật ExpressionOptions
    //    .IgnoreCaseAtBuiltInFunctions.
    // 2) NCalc KHÔNG có hàm AND/OR/NOT kiểu Excel sẵn (chỉ có operator &&/||), nên công thức
    //    dùng AND(...)/OR(...)/NOT(...) luôn ném NCalcFunctionNotFoundException nếu không tự
    //    đăng ký qua expr.Functions[...] (không phải event EvaluateFunction như bản NCalc cũ).
    // 3) Công thức Excel copy nguyên thường có dấu "=" ở đầu (ví dụ "=ROUND(...)") — NCalc
    //    không parse được dấu "=" đứng đầu (NCalcParserException), phải cắt bỏ trước khi
    //    đưa vào Expression.
    public static class CongThucEvaluator
    {
        public const string TenBien = "H";

        private const ExpressionOptions TuyChonMacDinh =
            ExpressionOptions.RoundAwayFromZero | ExpressionOptions.IgnoreCaseAtBuiltInFunctions;

        /// <summary>Tính thử/áp dụng công thức với 1 giá trị nhập. Trả về (null, lỗi) nếu
        /// công thức sai cú pháp, dùng biến lạ, hoặc kết quả không xác định (vd chia 0).</summary>
        public static (decimal? KetQua, string? Loi) Evaluate(string? congThuc, decimal giaTriNhap)
        {
            if (string.IsNullOrWhiteSpace(congThuc))
                return (null, "Công thức không được để trống.");

            try
            {
                var bieuThuc = congThuc.Trim();
                if (bieuThuc.StartsWith('='))
                    bieuThuc = bieuThuc[1..];

                var expr = new Expression(bieuThuc, TuyChonMacDinh);
                expr.Parameters[TenBien] = (double)giaTriNhap;
                DangKyHamLogicExcel(expr);
                var raw = expr.Evaluate();
                return ToDecimalResult(raw);
            }
            catch (Exception ex)
            {
                return (null, DienGiaiLoi(ex));
            }
        }

        /// <summary>Chỉ kiểm tra công thức có hợp lệ không (dùng khi lưu danh mục) — tính thử
        /// với 1 giá trị mẫu bất kỳ (1) để bắt lỗi cú pháp/biến lạ sớm.</summary>
        public static string? Validate(string? congThuc)
        {
            var (ketQua, loi) = Evaluate(congThuc, 1m);
            if (loi != null) return loi;
            if (ketQua == null) return "Công thức không trả về được giá trị với dữ liệu mẫu.";
            return null;
        }

        /// <summary>Bổ sung AND/OR/NOT kiểu Excel — NCalc không có sẵn các hàm này, chỉ có
        /// operator &&/||. Đăng ký cả biến thể chữ hoa/thường vì Functions không áp dụng
        /// IgnoreCaseAtBuiltInFunctions (tuỳ chọn đó chỉ gộp các hàm built-in).</summary>
        private static void DangKyHamLogicExcel(Expression expr)
        {
            foreach (var ten in new[] { "AND", "And", "and" })
                expr.Functions[ten] = args => TatCa(args, true);
            foreach (var ten in new[] { "OR", "Or", "or" })
                expr.Functions[ten] = args => TatCa(args, false);
            foreach (var ten in new[] { "NOT", "Not", "not" })
                expr.Functions[ten] = args => !Convert.ToBoolean(args.Evaluate(0));
        }

        private static bool TatCa(FunctionData args, bool laAnd)
        {
            for (var i = 0; i < args.Count; i++)
            {
                var giaTri = Convert.ToBoolean(args.Evaluate(i));
                if (laAnd && !giaTri) return false;
                if (!laAnd && giaTri) return true;
            }
            return laAnd;
        }

        private static (decimal?, string?) ToDecimalResult(object? raw)
        {
            if (raw == null) return (null, "Công thức không trả về giá trị.");
            if (raw is bool) return (null, "Công thức phải trả về một số (có vẻ đang trả về đúng/sai).");
            if (raw is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                    return (null, "Kết quả không xác định với giá trị nhập này (có thể do chia cho 0).");
            }
            try
            {
                return (Convert.ToDecimal(raw), null);
            }
            catch (OverflowException)
            {
                return (null, "Kết quả tính ra vượt phạm vi cho phép.");
            }
            catch (InvalidCastException)
            {
                return (null, "Công thức không trả về được một giá trị số hợp lệ.");
            }
        }

        private static string DienGiaiLoi(Exception ex)
        {
            var typeName = ex.GetType().Name;
            if (typeName.Contains("ParameterNotDefined", StringComparison.OrdinalIgnoreCase))
                return $"Công thức dùng biến không xác định — chỉ được dùng biến '{TenBien}' (đại diện giá trị nhập). Chi tiết: {ex.Message}";
            if (typeName.Contains("FunctionNotFound", StringComparison.OrdinalIgnoreCase))
                return $"Công thức dùng hàm không được hỗ trợ: {ex.Message}";
            if (typeName.Contains("Parser", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Syntax", StringComparison.OrdinalIgnoreCase))
                return $"Công thức sai cú pháp: {ex.Message}";
            return $"Công thức không hợp lệ: {ex.Message}";
        }
    }
}
