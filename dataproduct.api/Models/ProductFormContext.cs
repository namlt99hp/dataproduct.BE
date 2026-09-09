using System;
using System.Collections.Generic;
using System.Globalization;
using dataproduct.api.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Models;

public partial class ProductFormContext : DbContext
{
    public ProductFormContext()
    {
    }

    public ProductFormContext(DbContextOptions<ProductFormContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BkNguyenLieu> BkNguyenLieus { get; set; }

    public virtual DbSet<BkKcscanBbxlSanxuat> BkKcscanBbxlSanxuats { get; set; }

    public virtual DbSet<BkPhoiThep> BkPhoiThep { get; set; }

    public virtual DbSet<BmPheDuyet> BmPheDuyets { get; set; }

    public virtual DbSet<BmPhieu> BmPhieus { get; set; }

    public virtual DbSet<BmPhieuChiTiet> BmPhieuChiTiets { get; set; }

    public virtual DbSet<BmQuyenXl> BmQuyenXls { get; set; }

    public virtual DbSet<CtdPhoiNguoi> CtdPhoiNguois { get; set; }

    public virtual DbSet<CtdPhoiNong> CtdPhoiNongs { get; set; }

    public virtual DbSet<CtdPhieuXuLyKph> CtdPhieuXuLyKphs { get; set; }

    public virtual DbSet<CtdSoTheoDoi> CtdSoTheoDois { get; set; }

    public virtual DbSet<CtdStdDienBien> CtdStdDienBiens { get; set; }

    public virtual DbSet<CtdGiaoNhanPhoi> CtdGiaoNhanPhois { get; set; }

    public virtual DbSet<DLNM_HRC2> DLNM_HRC2s { get; set; }

    public virtual DbSet<Header_Key> Header_Keys { get; set; }

    public virtual DbSet<Header_Nhom> Header_Nhoms { get; set; }

    public virtual DbSet<Header_Mapping> Header_Mappings { get; set; }

    public virtual DbSet<HRC2_NM> HRC2_NMs { get; set; }
    public virtual DbSet<PhuLieu_NM> PhuLieu_NMs { get; set; }
    public virtual DbSet<PhuLieu_HRC2> PhuLieu_HRC2s { get; set; }
    public virtual DbSet<Hrc1TieuHao> Hrc1TieuHaos { get; set; }
    public virtual DbSet<Hrc1PhuLieu> Hrc1PhuLieus { get; set; }
    public virtual DbSet<Hrc1PhuLieuNm> Hrc1PhuLieuNms { get; set; }
    public virtual DbSet<STD_XUAT_NHAP_TON_HRC2> STD_XUAT_NHAP_TON_HRC2s { get; set; }
    public virtual DbSet<STD_NXT_TOTAL_HRC2> STD_NXT_TOTAL_HRC2s { get; set; }
    public virtual DbSet<STD_XUAT_NHAP_TON_HRC1> STD_XUAT_NHAP_TON_HRC1s { get; set; }
    public virtual DbSet<STD_NXT_TOTAL_HRC1> STD_NXT_TOTAL_HRC1s { get; set; }
    public virtual DbSet<STD_NXT_Filter> STD_NXT_Filters { get; set; }
    public virtual DbSet<STD_NXT_Filter_Init> STD_NXT_Filter_Inits { get; set; }

    public virtual DbSet<BM_SanLuongPhoi> BM_SanLuongPhoi { get; set; }
    public virtual DbSet<BM_PhoiNhapKho> BM_PhoiNhapKho { get; set; }
    public virtual DbSet<BkKcsBbxnSanLuong> BkKcsBbxnSanLuongs { get; set; }
    public virtual DbSet<NL_BTDBenPhe> NL_BTDBenPhes { get; set; }
    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=192.168.240.3,1433;Database=PRODUCT_FORM;User Id=sa;Password=HPDQ@1234;TrustServerCertificate=True;");
    public virtual DbSet<Silo> Silos { get; set; }
    public virtual DbSet<BmKiemKePhuLieu> BmKiemKePhuLieus { get; set; }
    public virtual DbSet<MapSiloPhuLieuNM> MapSiloPhuLieuNMs { get; set; }
    public virtual DbSet<BBGN_ThepLong> BBGN_ThepLongs { get; set; }
    public virtual DbSet<MacThep> MacTheps { get; set; }
    public virtual DbSet<MayDuc> MayDucs { get; set; }
    public virtual DbSet<MacThep_MayDuc> MacThep_MayDucs { get; set; }
    public virtual DbSet<NhomPhanLoaiMacThep> NhomPhanLoaiMacTheps {get; set;}

    // HRC2 Slab
    public virtual DbSet<BkHrc2Slab> BkHrc2Slabs { get; set; }
    public virtual DbSet<BkHrc2SlabTrangThai> BkHrc2SlabTrangThais { get; set; }
    public virtual DbSet<BkSyncHrc2SlabControl> BkSyncHrc2SlabControls { get; set; }
    public virtual DbSet<BkHrc2Slab_UserCheck> BkHrc2Slab_UserChecks { get; set; }

    // HRC1
    public virtual DbSet<HRC1_MeThep> HRC1_MeTheps { get; set; }
    public virtual DbSet<HRC1_MePhanCong> HRC1_MePhanCongs { get; set; }
    public virtual DbSet<HRC1_LichSu> HRC1_LichSus { get; set; }

    // HRC1 Slab
    public virtual DbSet<Hrc1Slab> Hrc1Slabs { get; set; }
    public virtual DbSet<Hrc1SlabTrangThai> Hrc1SlabTrangThais { get; set; }
    public virtual DbSet<Hrc1SlabEdit> Hrc1SlabEdits { get; set; }
    public virtual DbSet<MaVatTu> MaVatTus { get; set; }
    public virtual DbSet<Hrc1MaVatTu> Hrc1MaVatTus { get; set; }
    public virtual DbSet<Hrc1BbslTongHopGhiChu> Hrc1BbslTongHopGhiChus { get; set; }
    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=192.168.240.3,1433;Database=PRODUCT_FORM;User Id=sa;Password=HPDQ@1234;TrustServerCertificate=True;");
    public virtual DbSet<LG_NL_SiLo> LG_NL_SiLo { get; set; }
    public virtual DbSet<LG_NL_Mapping> LG_NL_Mapping { get; set; }
    public virtual DbSet<LG_NL_NVL> LG_NL_NVL { get; set; }
    public virtual DbSet<LG_NL_NhomNVL> LG_NL_NhomNVL { get; set; }
    public virtual DbSet<LG_NL_ChiTiet> LG_NL_ChiTiet { get; set; }
    public virtual DbSet<LG1_NL_TS_Mapping> LG1_NL_TS_Mapping { get; set; }
    public virtual DbSet<LG1_DuLieuNL> LG1_DuLieuNL { get; set; }
    public virtual DbSet<LG_TSL_NVL> LG_TSL_NVL { get; set; }
    public virtual DbSet<LG_TSL_SiLo> LG_TSL_SiLo { get; set; }
    public virtual DbSet<LG_TSL_SiLo_Mapping> LG_TSL_SiLo_Mapping { get; set; }
    public virtual DbSet<LG_TSL_ChiTiet> LG_TSL_ChiTiet { get; set; }
    public virtual DbSet<SiLoTon> SiLoTon { get; set; }
    public virtual DbSet<LG_NKVHPT_DuLieu> LG_NKVHPT_DuLieu { get; set; }
    public virtual DbSet<LG_NKVHPT_ChiTiet> LG_NKVHPT_ChiTiet { get; set; }
    public virtual DbSet<LG_PB_NhomPhanBo> LG_PB_NhomPhanBo { get; set; }
    public virtual DbSet<LG_PB_NVL_NhomPhanBo> LG_PB_NVL_NhomPhanBo { get; set; }
    public virtual DbSet<LG_PB_Map_Xuong_LoCao> LG_PB_Map_Xuong_LoCao { get; set; }
    public virtual DbSet<LG_PB_BienBanNhanQHLCCVH> LG_PB_BienBanNhanQHLCCVH { get; set; }
    public virtual DbSet<LG_PB_TyLePhanBo> LG_PB_TyLePhanBo { get; set; }
    public virtual DbSet<LG_PB_TyLeNhom> LG_PB_TyLeNhom { get; set; }
    public virtual DbSet<LG_PB_KetQuaPhanBo> LG_PB_KetQuaPhanBo { get; set; }
    public virtual DbSet<LG_PB_Map_NvlCVH_LoCao> LG_PB_Map_NvlCVH_LoCao { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {


        modelBuilder.Entity<BkNguyenLieu>(entity =>
        {
            entity.ToTable("BK_NguyenLieu");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.DoAm).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.GhiChu).HasMaxLength(50);
            entity.Property(e => e.GioLayMau).HasMaxLength(10);
            entity.Property(e => e.GioNhapBk)
                .HasMaxLength(50)
                .HasColumnName("GioNhap_BK");
            entity.Property(e => e.Kip)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.NgaySx).HasColumnName("NgaySX");
            entity.Property(e => e.Silo).HasMaxLength(20);
            entity.Property(e => e.TenNvl)
                .HasMaxLength(150)
                .HasColumnName("TenNVL");
            entity.Property(e => e.TronId).HasColumnName("Tron_ID");
        });

        modelBuilder.Entity<BkKcscanBbxlSanxuat>(entity =>
        {
            entity.ToTable("BK_KCSCAN_BBXL_SANXUAT");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.WorkshopName).HasMaxLength(100).HasColumnName("WorkshopName");
            entity.Property(e => e.Order).HasMaxLength(100).HasColumnName("Order");
            entity.Property(e => e.ProcessProductionDate).HasColumnName("ProcessProductionDate");
            entity.Property(e => e.ProcessShiftName).HasMaxLength(20).HasColumnName("ProcessShiftName");
            entity.Property(e => e.NewProductName).HasMaxLength(100).HasColumnName("NewProductName");
            entity.Property(e => e.Product).HasMaxLength(100).HasColumnName("Product");
            entity.Property(e => e.NewGradeCode).HasMaxLength(50).HasColumnName("NewGradeCode");
            entity.Property(e => e.NewLength).HasColumnType("float").HasColumnName("NewLength");
            entity.Property(e => e.NewNumOfBar).HasColumnName("NewNumOfBar");
            entity.Property(e => e.NewWeight).HasColumnType("float").HasColumnName("NewWeight");
            entity.Property(e => e.NewClassifyCode).HasMaxLength(50).HasColumnName("NewClassifyCode");
            entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("Reason");
            entity.Property(e => e.Measures).HasMaxLength(255).HasColumnName("Measures");
            entity.Property(e => e.InProductName).HasMaxLength(100).HasColumnName("InProductName");
            entity.Property(e => e.InProduct).HasMaxLength(100).HasColumnName("InProduct");
            entity.Property(e => e.InGradeCode).HasMaxLength(50).HasColumnName("InGradeCode");
            entity.Property(e => e.InLength).HasColumnType("float").HasColumnName("InLength");
            entity.Property(e => e.InNumOfBar).HasColumnName("InNumOfBar");
            entity.Property(e => e.InWeight).HasColumnType("float").HasColumnName("InWeight");
            entity.Property(e => e.InClassifyCode).HasMaxLength(10).HasColumnName("InClassifyCode");
            entity.Property(e => e.InShiftName).HasMaxLength(50).HasColumnName("InShiftName");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasColumnName("CreatedAt");
            entity.Property(e => e.NgayXL).HasColumnName("NgayXL");
            entity.Property(e => e.CaXL).HasMaxLength(20).HasColumnName("CaXL");
            entity.Property(e => e.XuongCan).HasColumnName("XuongCan");
        });

        modelBuilder.Entity<BkPhoiThep>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BK_NuocThep");

            entity.ToTable("BK_PhoiThep");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.GhiChu).HasMaxLength(50);
            entity.Property(e => e.KichThuoc).HasMaxLength(20);
            entity.Property(e => e.Kip)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.LoaiId).HasColumnName("LoaiID");
            entity.Property(e => e.LoaiPhoi).HasMaxLength(50);
            entity.Property(e => e.Mac).HasMaxLength(10);
            entity.Property(e => e.MauThu).HasMaxLength(10);
            entity.Property(e => e.Me).HasMaxLength(20);
            entity.Property(e => e.NgaySx).HasColumnName("NgaySX");
            entity.Property(e => e.NgayTaoBk)
                .HasColumnType("datetime")
                .HasColumnName("NgayTaoBK");
            entity.Property(e => e.StDaChuyen).HasColumnName("ST_DaChuyen");
            entity.Property(e => e.TenLoai).HasMaxLength(10);
            entity.Property(e => e.TenPhanLoai).HasMaxLength(20);
            entity.Property(e => e.VanChuyen).HasMaxLength(50);
            entity.Property(e => e.IsNm).HasColumnName("IsNM");
        });

        modelBuilder.Entity<BmPheDuyet>(entity =>
        {
            entity.ToTable("BM_PheDuyet");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.GhiChu).HasMaxLength(250);
            entity.Property(e => e.NgayDuyet).HasColumnType("datetime");
            entity.Property(e => e.NguoiDuyetId).HasColumnName("NguoiDuyetID");
            entity.Property(e => e.PhieuId).HasColumnName("PhieuID");
        });

        modelBuilder.Entity<BmPhieu>(entity =>
        {
            entity.HasKey(e => e.Idphieu);

            entity.ToTable("BM_Phieu");

            entity.Property(e => e.Idphieu)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("IDPhieu");
            entity.Property(e => e.DataJson).HasColumnName("DataJSon");
            entity.Property(e => e.Idkip).HasColumnName("IDKip");
            entity.Property(e => e.IdphongBan).HasColumnName("IDPhongBan");
            entity.Property(e => e.IsDelete).HasDefaultValue(0);
            entity.Property(e => e.IsLock).HasDefaultValue(0);
            entity.Property(e => e.Kip)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.MaBm)
                .HasMaxLength(100)
                .HasColumnName("MaBM");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.NgayTao).HasColumnType("datetime");
            entity.Property(e => e.NguoiTaoId).HasColumnName("NguoiTaoID");
            entity.Property(e => e.SoPhieu).HasMaxLength(50);
            entity.Property(e => e.XuongId).HasColumnName("XuongID");
        });

        modelBuilder.Entity<BmPhieuChiTiet>(entity =>
        {
            entity.ToTable("BM_Phieu_ChiTiet");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.GiaTri).HasMaxLength(500);
            entity.Property(e => e.PhieuId).HasColumnName("PhieuID");
            entity.Property(e => e.RowId).HasColumnName("RowID");
            entity.Property(e => e.ThongSo).HasMaxLength(20);
        });

        modelBuilder.Entity<BmQuyenXl>(entity =>
        {
            entity.ToTable("BM_QuyenXL");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.MaBm)
                .HasMaxLength(50)
                .HasColumnName("MaBM");
            entity.Property(e => e.MaKhuVuc).HasMaxLength(20);
            entity.Property(e => e.QuyenChucNang).HasColumnName("QuyenChucNang");
            entity.Property(e => e.KhuVucPhu).HasColumnName("KhuVucPhu");

        });

        modelBuilder.Entity<CtdPhoiNguoi>(entity =>
        {
            entity.ToTable("CTD_PhoiNguoi");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.GhiChu).HasMaxLength(250);
            entity.Property(e => e.KichThuoc).HasMaxLength(50);
            entity.Property(e => e.Mac).HasMaxLength(10);
            entity.Property(e => e.Me).HasMaxLength(10);
            entity.Property(e => e.PhieuId).HasColumnName("PhieuID");
            entity.Property(e => e.TongKl).HasColumnName("TongKL");
        });

        modelBuilder.Entity<CtdPhoiNong>(entity =>
        {
            entity.ToTable("CTD_PhoiNong");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CaKip)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.GhiChu).HasMaxLength(200);
            entity.Property(e => e.IdBkPhoiThep).HasColumnName("ID_BK_PhoiThep");
            entity.Property(e => e.Idphieu).HasColumnName("IDPhieu");
            entity.Property(e => e.KhoiLuongLoai1).HasColumnName("KhoiLuong_Loai1");
            entity.Property(e => e.KhoiLuongLoai2).HasColumnName("KhoiLuong_Loai2");
            entity.Property(e => e.KhoiLuongLoai3).HasColumnName("KhoiLuong_Loai3");
            entity.Property(e => e.KichThuoc).HasMaxLength(50);
            entity.Property(e => e.Kip)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e.Mac).HasMaxLength(10);
            entity.Property(e => e.Me).HasMaxLength(10);
            entity.Property(e => e.NgaySx).HasColumnName("NgaySX");
            entity.Property(e => e.NgayTao).HasColumnType("datetime");
            entity.Property(e => e.NmCan).HasColumnName("NMCan");
            entity.Property(e => e.SoThanhLoai1).HasColumnName("SoThanh_Loai1");
            entity.Property(e => e.SoThanhLoai2).HasColumnName("SoThanh_Loai2");
            entity.Property(e => e.SoThanhLoai3).HasColumnName("SoThanh_Loai3");
            entity.Property(e => e.TinhTrangCTD).HasColumnName("TinhTrangCTD");
            entity.Property(e => e.TinhTrangQLCL).HasColumnName("TinhTrangQLCL");
            entity.Property(e => e.TongKl).HasColumnName("TongKL");
            entity.Property(e => e.TongSt).HasColumnName("TongST");
        });

        modelBuilder.Entity<CtdPhieuXuLyKph>(entity =>
        {
            entity.ToTable("CTD_Phieu_XuLyKPH");

            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.IdPhieu).HasColumnName("IdPhieu");
            entity.Property(e => e.InSanPham).HasMaxLength(100);
            entity.Property(e => e.InMacThep).HasMaxLength(50);
            entity.Property(e => e.InChieuDai).HasMaxLength(50);
            entity.Property(e => e.InSoMe).HasMaxLength(100);
            entity.Property(e => e.InSoThanh);
            entity.Property(e => e.InKhoiLuong).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InCaNgaySx)
                .HasMaxLength(100)
                .HasColumnName("InCaNgaySX");
            entity.Property(e => e.InLoai).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Measures).HasMaxLength(500);
            entity.Property(e => e.NewSanPham).HasMaxLength(100);
            entity.Property(e => e.NewMacThep).HasMaxLength(50);
            entity.Property(e => e.NewChieuDai).HasMaxLength(50);
            entity.Property(e => e.NewSoMe).HasMaxLength(100);
            entity.Property(e => e.NewSoThanh);
            entity.Property(e => e.NewKhoiLuong).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NewLoai).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NgayXL).HasColumnName("NgayXL");
            entity.Property(e => e.CaXL).HasColumnName("CaXL");
            entity.Property(e => e.KipXL).HasMaxLength(50).HasColumnName("KipXL");
            entity.Property(e => e.LenhSanXuat).HasMaxLength(50).HasColumnName("LenhSanXuat");
        });

        modelBuilder.Entity<CtdSoTheoDoi>(entity =>
        {
            entity.ToTable("CTD_SoTheoDoi");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Idphieu).HasColumnName("IDPhieu");
            entity.Property(e => e.LoaiMacPhoi).HasColumnName("LoaiMacPhoi");
            entity.Property(e => e.KichThuoc).HasMaxLength(50);
            entity.Property(e => e.PhoiRaLo).HasColumnName("PhoiRaLo");
            entity.Property(e => e.PhoiHoiLo).HasColumnName("PhoiHoiLo");
            entity.Property(e => e.PhoiRaSan).HasColumnName("PhoiRaSan");
            entity.Property(e => e.PhoiPheCn).HasColumnName("PhoiPheCN");
            entity.Property(e => e.LoaiSp)
                .HasMaxLength(20)
                .HasColumnName("LoaiSP");
            entity.Property(e => e.LoaiPhoi).HasColumnName("LoaiPhoi");
            entity.Property(e => e.MacThep)
                .HasMaxLength(20)
                .HasColumnName("MacThep");
            entity.Property(e => e.LenhSanXuat).HasMaxLength(50);
        });

        modelBuilder.Entity<CtdStdDienBien>(entity =>
        {
            entity.ToTable("CTD_STD_DienBien");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Idphieu).HasColumnName("IDPhieu");
            entity.Property(e => e.TuGio).HasColumnName("TuGio");
            entity.Property(e => e.DenGio).HasColumnName("DenGio");
            entity.Property(e => e.ThietBi).HasMaxLength(50);
            entity.Property(e => e.MoTa)
                .HasMaxLength(250)
                .HasColumnName("MoTa");
            entity.Property(e => e.LoaiSuCo).HasMaxLength(50);
            entity.Property(e => e.PheCongNghe).HasMaxLength(50);
        });

        modelBuilder.Entity<CtdGiaoNhanPhoi>(entity =>
        {
            entity.ToTable("CTD_GiaoNhanPhoi");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.MaViTri).HasMaxLength(50);
            entity.Property(e => e.ViTri).HasMaxLength(200);
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.KichThuoc).HasMaxLength(50);
            entity.Property(e => e.SoCay).HasColumnName("SoCay");
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.IdPhieu).HasColumnName("IDPhieu");
        });

        modelBuilder.Entity<DLNM_HRC2>(entity =>
        {
            // HasTrigger: báo cho EF Core biết bảng có trigger (xem
            // .claude/migrations/hrc2_header_key_chiphi_productiondata.sql) để KHÔNG dùng OUTPUT clause khi
            // INSERT/UPDATE — SQL Server không cho phép OUTPUT (không INTO) trên bảng có trigger.
            entity.ToTable("DLNM_HRC2", tb =>
            {
                tb.HasTrigger("trg_DLNM_HRC2_ChiPhi_SoftDelete");
                tb.HasTrigger("trg_DLNM_HRC2_ChiPhi_GangLong");
            });
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.REPORT_NO).HasColumnName("REPORT_NO");
            entity.Property(e => e.NgaySx).HasColumnName("NgaySX");
            entity.Property(e => e.Ngay).HasColumnName("Ngay");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.MeThoi).HasColumnName("MeThoi");
            entity.Property(e => e.MacThep).HasColumnName("MacThep");
            entity.Property(e => e.MacThep_Manual).HasColumnName("MacThep_Manual");
            entity.Property(e => e.IsManualMacThep).HasColumnName("IsManualMacThep");
            entity.Property(e => e.O2).HasColumnName("O2");
            entity.Property(e => e.AR_RH).HasColumnName("AR_RH");
            entity.Property(e => e.N2).HasColumnName("N2");
            entity.Property(e => e.AR_BOF).HasColumnName("AR_BOF");
            entity.Property(e => e.AR_LF).HasColumnName("AR_LF");
            entity.Property(e => e.KLGangLong).HasColumnName("KLGangLong");
            entity.Property(e => e.KLThepPhe).HasColumnName("KLThepPhe");
            entity.Property(e => e.KLThepPheGang).HasColumnName("KLThepPheGang");
        });

        modelBuilder.Entity<Header_Key>(entity =>
        {
            entity.ToTable("Header_Key");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.KeyGuid).HasColumnName("KeyGuid");
            entity.Property(e => e.TenHienThi).HasColumnName("TenHienThi");
            entity.Property(e => e.Mota).HasColumnName("Mota");
            entity.Property(e => e.LoaiPhieu).HasColumnName("LoaiPhieu");
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.IsUsedNXT).HasColumnName("IsUsedNXT");
            entity.Property(e => e.TyTrong)
                .HasColumnName("TyTrong")
                .HasPrecision(18, 3);
            entity.Property(e => e.IsUsedThongKe).HasColumnName("IsUsedThongKe");
            entity.Property(e => e.LoaiThongKe).HasColumnName("LoaiThongKe");
        });

        modelBuilder.Entity<Header_Nhom>(entity =>
        {
            entity.ToTable("Header_Nhom");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.TenHienThi).HasColumnName("TenHienThi");
            entity.Property(e => e.TenNhom).HasColumnName("TenNhom");
        });

        modelBuilder.Entity<Header_Mapping>(entity =>
        {
            entity.ToTable("Header_Mapping");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.TenNguonDuLieu).HasColumnName("TenNguonDuLieu");
            entity.Property(e => e.ID_PhuLieu).HasColumnName("ID_PhuLieu");
            entity.Property(e => e.ID_HeaderKey).HasColumnName("ID_HeaderKey");
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
        });

        modelBuilder.Entity<HRC2_NM>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("HRC2_NM");
            entity.Property(e => e.REPORT_NO).HasColumnName("REPORT_NO");
            entity.Property(e => e.PRODUCTION_DATE).HasColumnName("PRODUCTION_DATE");
            entity.Property(e => e.ShiftDate).HasColumnName("ShiftDate");
            entity.Property(e => e.Shift).HasColumnName("Shift");
            entity.Property(e => e.PLANT).HasColumnName("PLANT");
            entity.Property(e => e.PLANT_NO).HasColumnName("PLANT_NO");
            entity.Property(e => e.PRODUCT_ID).HasColumnName("PRODUCT_ID");
            entity.Property(e => e.GRADE_ID_PLAN).HasColumnName("GRADE_ID_PLAN");
            entity.Property(e => e.O2).HasColumnName("O2");
            entity.Property(e => e.AR_RH).HasColumnName("AR_RH");
            entity.Property(e => e.N2).HasColumnName("N2");
            entity.Property(e => e.AR_BOF).HasColumnName("AR_BOF");
            entity.Property(e => e.AR_LF).HasColumnName("AR_LF");
            entity.Property(e => e.MATERIAL_NO)
                .HasColumnName("MATERIAL_NO")
                .HasPrecision(18, 0);
            entity.Property(e => e.DESCRIPTION_EN).HasColumnName("DESCRIPTION_EN");
            entity.Property(e => e.KLPhuGia).HasColumnName("KLPhuGia");
            entity.Property(e => e.KLGangLong).HasColumnName("KLGangLong");
            entity.Property(e => e.KLThepPhe).HasColumnName("KLThepPhe");
            entity.Property(e => e.PLANT_NO)
                .HasColumnName("PLANT_NO")
                .HasPrecision(18, 0);
            entity.Property(e => e.REPORT_NO)
                .HasColumnName("REPORT_NO")
                .HasPrecision(18, 0);
        });
        modelBuilder.Entity<PhuLieu_HRC2>(entity =>
        {
            entity.ToTable("PhuLieu_HRC2", tb =>
            {
                tb.HasTrigger("TR_PhuLieu_HRC2_Upsert_PhuLieu_NM");
                // trg_PhuLieu_HRC2_ChiPhi: xem .claude/migrations/hrc2_header_key_chiphi_productiondata.sql
                tb.HasTrigger("trg_PhuLieu_HRC2_ChiPhi");
            });
            entity.Property(e => e.ID).HasColumnName("Id");
            entity.Property(e => e.REPORT_NO).HasColumnName("REPORT_NO");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.MeThoi).HasColumnName("MeThoi");
            entity.Property(e => e.ID_PhuLieu).HasColumnName("ID_PhuLieu");
            entity.Property(e => e.TenPhuLieu).HasColumnName("TenPhuLieu");
            entity.Property(e => e.KLPhuGia).HasColumnName("KLPhuGia");
            entity.Property(e => e.ID_HeaderKey).HasColumnName("ID_HeaderKey");
            entity.Property(e => e.TenHienThi).HasColumnName("TenHienThi");
            entity.Property(e => e.IsManual).HasColumnName("IsManual");
            entity.Property(e => e.KLPhuGia_Manual).HasColumnName("KLPhuGia_Manual");
        });

        modelBuilder.Entity<Hrc1TieuHao>(entity =>
        {
            entity.ToTable("HRC1_TieuHao", tb =>
            {
                tb.HasTrigger("trg_HRC1_TieuHao_ChiPhi_SoftDelete");
                tb.HasTrigger("trg_HRC1_TieuHao_ChiPhi_GangLong");
            });
            entity.HasKey(e => e.ID);
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.IDNM).HasColumnName("IDNM");
            entity.Property(e => e.IsNM).HasColumnName("IsNM");
            entity.Property(e => e.IsEdited).HasColumnName("IsEdited");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.MeThoi).HasColumnName("MeThoi");
            entity.Property(e => e.MacThep).HasColumnName("MacThep");
            entity.Property(e => e.MacThepOrig).HasColumnName("MacThepOrig");
            entity.Property(e => e.MacThepIsManual).HasColumnName("MacThepIsManual");
            entity.Property(e => e.O2).HasColumnName("O2");
            entity.Property(e => e.N2).HasColumnName("N2");
            entity.Property(e => e.AR).HasColumnName("AR");
            entity.Property(e => e.IsChuyenCa).HasColumnName("IsChuyenCa");
            entity.Property(e => e.CaChuyen).HasColumnName("CaChuyen");
            entity.Property(e => e.IsTrungMeThoi).HasColumnName("IsTrungMeThoi");
            entity.Property(e => e.QueLayMau).HasColumnName("QueLayMau");
            entity.Property(e => e.QueDoNhiet).HasColumnName("QueDoNhiet");
            entity.Property(e => e.GhiChu).HasColumnName("GhiChu");
            entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
            entity.Property(e => e.NgayXoa).HasColumnName("NgayXoa");
            entity.Property(e => e.NguoiXoa).HasColumnName("NguoiXoa");
            entity.Property(e => e.ThoiDiemKetThuc).HasColumnName("ThoiDiemKetThuc");
            entity.Property(e => e.KLGang).HasColumnName("KLGang").HasPrecision(18, 3);
            entity.Property(e => e.KLGangLongCCT).HasColumnName("KLGangLongCCT").HasPrecision(18, 3);
            entity.Property(e => e.KLThepPhe).HasColumnName("KLThepPhe").HasPrecision(18, 3);
            entity.Property(e => e.KLThepPheOrig).HasColumnName("KLThepPheOrig").HasPrecision(18, 3);
            entity.Property(e => e.KLThepPheIsManual).HasColumnName("KLThepPheIsManual");
            entity.Property(e => e.KLThepPheGang).HasColumnName("KLThepPheGang").HasPrecision(18, 3);
            entity.Property(e => e.KLThepLong).HasColumnName("KLThepLong").HasPrecision(18, 3);
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.NgaySanXuat).HasColumnName("NgaySanXuat");
            entity.Property(e => e.ThoiDiemBatDau).HasColumnName("ThoiDiemBatDau");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.NguoiTao).HasColumnName("NguoiTao");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NgayCapNhat");
            entity.Property(e => e.NguoiCapNhat).HasColumnName("NguoiCapNhat");
            entity.Property(e => e.IDPhieu).HasColumnName("IDPhieu");
            entity.Property(e => e.SourceIDNM).HasColumnName("SourceIDNM");
            entity.Property(e => e.IDMeThep).HasColumnName("IDMeThep");
        });

        modelBuilder.Entity<Hrc1PhuLieu>(entity =>
        {
            entity.ToTable("HRC1_PhuLieu", tb => tb.HasTrigger("trg_HRC1_PhuLieu_ChiPhi"));
            entity.HasKey(e => e.ID);
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.MeID).HasColumnName("MeID");
            entity.Property(e => e.PhuLieuID).HasColumnName("PhuLieuID");
            entity.Property(e => e.TenPhuLieu).HasColumnName("TenPhuLieu");
            entity.Property(e => e.KLPhuGia).HasColumnName("KLPhuGia").HasPrecision(18, 3);
            entity.Property(e => e.KLPhanBo).HasColumnName("KLPhanBo").HasPrecision(18, 3);
            entity.Property(e => e.ID_HeaderKey).HasColumnName("ID_HeaderKey");
            entity.Property(e => e.IsManual).HasColumnName("IsManual");
            entity.Property(e => e.KLPhuGia_Manual).HasColumnName("KLPhuGia_Manual").HasPrecision(18, 3);
            entity.Property(e => e.IsAddManual).HasColumnName("IsAddManual");
            entity.Property(e => e.IsPhanBo).HasColumnName("IsPhanBo");
            entity.Property(e => e.IsNM).HasColumnName("IsNM");
            entity.Property(e => e.IsEdited).HasColumnName("IsEdited");
            entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
            entity.Property(e => e.NguoiTao).HasColumnName("NguoiTao");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NgayCapNhat");
            entity.Property(e => e.NguoiCapNhat).HasColumnName("NguoiCapNhat");
        });

        modelBuilder.Entity<Hrc1PhuLieuNm>(entity =>
        {
            entity.ToTable("HRC1_PhuLieuNM");
            entity.HasKey(e => e.ID);
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.TenPhuLieu).HasColumnName("TenPhuLieu");
            entity.Property(e => e.TenPhuLieuNM).HasColumnName("TenPhuLieuNM");
            entity.Property(e => e.DangSuDung).HasColumnName("DangSuDung");
            entity.Property(e => e.IsNM).HasColumnName("IsNM");
            entity.Property(e => e.IsUsedNXT).HasColumnName("IsUsedNXT");
            entity.Property(e => e.MaVatTuChiPhi).HasColumnName("MaVatTuChiPhi");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.NguoiTao).HasColumnName("NguoiTao");
        });

        modelBuilder.Entity<STD_XUAT_NHAP_TON_HRC2>(entity =>
        {
            entity.ToTable("STD_XUAT_NHAP_TON_HRC2");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Id_HeaderKey).HasColumnName("Id_HeaderKey");
            entity.Property(e => e.TenNguyenLieu).HasColumnName("TenNguyenLieu");
            entity.Property(e => e.ViTri).HasColumnName("ViTri");
            entity.Property(e => e.TonDauCa).HasColumnName("TonDauCa");
            entity.Property(e => e.TuongQuanDauCa).HasColumnName("TuongQuanDauCa");
            entity.Property(e => e.NhapVaoTrongCa).HasColumnName("NhapVaoTrongCa");
            entity.Property(e => e.TonCuoiCa).HasColumnName("TonCuoiCa");
            entity.Property(e => e.TuongQuanCuoiCa).HasColumnName("TuongQuanCuoiCa");
            entity.Property(e => e.TongThucTe).HasColumnName("TongThucTe");
            entity.Property(e => e.Id_Phieu).HasColumnName("Id_Phieu");
            entity.Property(e => e.IDSilo).HasColumnName("IDSilo");
        });
        modelBuilder.Entity<STD_NXT_TOTAL_HRC2>(entity =>
        {
            entity.ToTable("STD_NXT_TOTAL_HRC2", tb =>
            {
                // trg_STD_NXT_TOTAL_HRC2_ChiPhi_PhanBo: xem .claude/migrations/hrc2_header_key_chiphi_productiondata.sql
                tb.HasTrigger("trg_STD_NXT_TOTAL_HRC2_ChiPhi_PhanBo");
            });
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.Id_HeaderKey).HasColumnName("Id_HeaderKey");
            entity.Property(e => e.TenNguyenLieu).HasColumnName("TenNguyenLieu");
            entity.Property(e => e.TongTonDauCa).HasColumnName("TongTonDauCa");
            entity.Property(e => e.TongTonNhapTrongCa).HasColumnName("TongTonNhapTrongCa");
            entity.Property(e => e.TongTonCuoiCa).HasColumnName("TongTonCuoiCa");
            entity.Property(e => e.TongSuDung).HasColumnName("TongSuDung");
            entity.Property(e => e.TongSDTrenSoSach).HasColumnName("TongSDTrenSoSach");
            entity.Property(e => e.ChenhLech).HasColumnName("ChenhLech");
            entity.Property(e => e.Id_Phieu).HasColumnName("Id_Phieu");
            entity.Property(e => e.HasPhanBo).HasColumnName("HasPhanBo");
        });
        modelBuilder.Entity<STD_XUAT_NHAP_TON_HRC1>(entity =>
        {
            entity.ToTable("STD_XUAT_NHAP_TON_HRC1");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.PhuLieuID).HasColumnName("PhuLieuID");
            entity.Property(e => e.TenNguyenLieu).HasColumnName("TenNguyenLieu");
            entity.Property(e => e.ViTri).HasColumnName("ViTri");
            entity.Property(e => e.TonDauCa).HasColumnName("TonDauCa");
            entity.Property(e => e.TuongQuanDauCa).HasColumnName("TuongQuanDauCa");
            entity.Property(e => e.NhapVaoTrongCa).HasColumnName("NhapVaoTrongCa");
            entity.Property(e => e.TonCuoiCa).HasColumnName("TonCuoiCa");
            entity.Property(e => e.TuongQuanCuoiCa).HasColumnName("TuongQuanCuoiCa");
            entity.Property(e => e.TongThucTe).HasColumnName("TongThucTe");
            entity.Property(e => e.Id_Phieu).HasColumnName("Id_Phieu");
            entity.Property(e => e.IDSilo).HasColumnName("IDSilo");
        });
        modelBuilder.Entity<STD_NXT_TOTAL_HRC1>(entity =>
        {
            entity.ToTable("STD_NXT_TOTAL_HRC1");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.PhuLieuID).HasColumnName("PhuLieuID");
            entity.Property(e => e.TenNguyenLieu).HasColumnName("TenNguyenLieu");
            entity.Property(e => e.TongTonDauCa).HasColumnName("TongTonDauCa");
            entity.Property(e => e.TongTonNhapTrongCa).HasColumnName("TongTonNhapTrongCa");
            entity.Property(e => e.TongTonCuoiCa).HasColumnName("TongTonCuoiCa");
            entity.Property(e => e.TongSuDung).HasColumnName("TongSuDung");
            entity.Property(e => e.TongSDTrenSoSach).HasColumnName("TongSDTrenSoSach");
            entity.Property(e => e.ChenhLech).HasColumnName("ChenhLech");
            entity.Property(e => e.Id_Phieu).HasColumnName("Id_Phieu");
            entity.Property(e => e.HasPhanBo).HasColumnName("HasPhanBo");
        });
        modelBuilder.Entity<STD_NXT_Filter>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("STD_NXT_Filter");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Scope)
                .HasColumnName("Scope")
                .HasPrecision(18, 0);
            entity.Property(e => e.ID_PhuLieu)
                .HasColumnName("ID_PhuLieu")
                .HasPrecision(18, 0);
            entity.Property(e => e.TenPhuLieu).HasColumnName("TenPhuLieu");
            entity.Property(e => e.TotalKLPhuGia).HasColumnName("TotalKLPhuGia");
        });
        modelBuilder.Entity<STD_NXT_Filter_Init>(entity =>
        {
            entity.HasNoKey();
            entity.ToTable("STD_NXT_Filter_Init");
            entity.Property(e => e.Id_HeaderKey).HasColumnName("Id_HeaderKey");
            entity.Property(e => e.TenNguyenLieu).HasColumnName("TenNguyenLieu");
            entity.Property(e => e.TonDauCa)
                .HasColumnName("TonDauCa")
                .HasPrecision(18, 3);
            entity.Property(e => e.NhapVaoTrongCa)
                .HasColumnName("NhapVaoTrongCa")
                .HasPrecision(18, 3);
            entity.Property(e => e.TonCuoiCa)
                .HasColumnName("TonCuoiCa")
                .HasPrecision(18, 3);
            entity.Property(e => e.Scope).HasColumnName("Scope");
        });
        modelBuilder.Entity<Silo>(entity =>
        {
            entity.ToTable("Silo");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TenSilo).HasColumnName("TenSilo");
            entity.Property(e => e.TheTich)
                .HasColumnName("TheTich")
                .HasPrecision(18, 3);
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.TinhTrang).HasColumnName("TinhTrang");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.NhaMay).HasColumnName("NhaMay");
        });
        modelBuilder.Entity<BmKiemKePhuLieu>(entity =>
        {
            entity.ToTable("BmKiemKePhuLieu");
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.ID_HeaderKey).HasColumnName("ID_HeaderKey");
            entity.Property(e => e.ID_PhuLieuNM).HasColumnName("ID_PhuLieuNM");
            entity.Property(e => e.ID_Silo).HasColumnName("ID_Silo");
            entity.Property(e => e.TheTich)
                .HasColumnName("TheTich")
                .HasPrecision(18, 3);
            entity.Property(e => e.TyTrong)
                .HasColumnName("TyTrong")
                .HasPrecision(18, 3);
        });
        modelBuilder.Entity<MapSiloPhuLieuNM>(entity =>
        {
            entity.ToTable("Map_Silo_PhuLieuNM");
            entity.Property(e => e.ID).HasColumnName("ID");
            entity.Property(e => e.ID_Silo).HasColumnName("ID_Silo");
            entity.Property(e => e.ID_PhuLieuNM).HasColumnName("ID_PhuLieuNM");
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
            entity.Property(e => e.NgayBatDau).HasColumnName("NgayBatDau");
            entity.Property(e => e.NgayKetThuc).HasColumnName("NgayKetThuc");
        });
        modelBuilder.Entity<BkKcsBbxnSanLuong>(entity =>
        {
            entity.ToTable("BK_KCS_BBXNSanLuong");
            entity.Property(e => e.Ca).HasMaxLength(50);
            entity.Property(e => e.SanPham).HasMaxLength(50);
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.ChieuDai).HasColumnType("float");
            entity.Property(e => e.SoBo).HasColumnName("SoBo");
            entity.Property(e => e.SoThanh).HasColumnType("decimal(32, 0)");
            entity.Property(e => e.KhoiLuong).HasColumnType("float");
            entity.Property(e => e.TenPhanLoai).HasMaxLength(50);
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.TenCa).HasMaxLength(50);
            entity.Property(e => e.IDXuongCan).HasMaxLength(50).HasColumnName("IDXuongCan");
            entity.Property(e => e.TenXuongCan).HasMaxLength(50);
            entity.Property(e => e.NgayTao).HasColumnType("datetime");
            entity.Property(e => e.IDPhieu).HasColumnName("IDPhieu");
        });

        modelBuilder.Entity<PhuLieu_NM>(entity =>
        {
            entity.ToTable("PhuLieu_NM");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ID_PhuLieu).HasColumnName("ID_PhuLieu");
            entity.Property(e => e.TenPhuLieu).HasColumnName("TenPhuLieu");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
            entity.Property(e => e.IsActive).HasColumnName("IsActive");
        });
        modelBuilder.Entity<BBGN_ThepLong>(entity =>
        {
            entity.ToTable("BBGN_ThepLong");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.MayDuc).HasColumnName("MayDuc");
            entity.Property(e => e.Me).HasColumnName("Me");
            entity.Property(e => e.MacThep).HasColumnName("MacThep");
            entity.Property(e => e.ThungSo).HasColumnName("ThungSo");
            entity.Property(e => e.ThoiGian).HasColumnName("ThoiGian");
            entity.Property(e => e.KlLan1).HasColumnName("KlLan1");
            entity.Property(e => e.KlLan2).HasColumnName("KlLan2");
            entity.Property(e => e.KlLan3).HasColumnName("KlLan3");
            entity.Property(e => e.KlThepLong).HasColumnName("KlThepLong");
            entity.Property(e => e.GhiChu).HasColumnName("GhiChu");
            entity.Property(e => e.TinhLuyenLenThang).HasColumnName("TinhLuyenLenThang");
            entity.Property(e => e.PhanLoai).HasColumnName("PhanLoai");
            entity.Property(e => e.NgaySX).HasColumnName("NgaySX");
            entity.Property(e => e.Ca).HasColumnName("Ca");
            entity.Property(e => e.BieuMau).HasColumnName("BieuMau");
            entity.Property(e => e.Scope).HasColumnName("Scope");
            entity.Property(e => e.IdPhieu).HasColumnName("IdPhieu");
            entity.Property(e => e.IsGhost).HasColumnName("IsGhost");
            entity.Property(e => e.IsTrungMeThoi).HasColumnName("IsTrungMeThoi");
            entity.Property(e => e.MacThepBKMIS).HasColumnName("MacThepBKMIS");
            entity.Property(e => e.IdMacThep).HasColumnName("IdMacThep");
            entity.Property(e => e.KLLFSauThep).HasColumnName("KLLFSauThep");
            entity.Property(e => e.IsThuNghiem).HasColumnName("IsThuNghiem");
            entity.Property(e => e.Kip).HasColumnName("Kip");
        });

        modelBuilder.Entity<MacThep>(entity =>
        {
            entity.ToTable("MacThep");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.TenMacThep).HasMaxLength(255);
            entity.Property(e => e.NhaMay).HasColumnName("NhaMay");
            entity.Property(e => e.IsLock).HasColumnName("IsLock");
            entity.Property(e => e.IsXacNhan).HasColumnName("IsXacNhan");
            entity.Property(e => e.Id_NhomPhanLoaiMacThep).HasColumnName("Id_NhomPhanLoaiMacThep");
            entity.Property(e => e.NgayTao).HasColumnName("NgayTao");
        });

        modelBuilder.Entity<MacThep_MayDuc>(entity =>
        {
            entity.ToTable("MacThep_MayDuc");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.IdMacThep).HasColumnName("IdMacThep");
            entity.Property(e => e.IdMayDuc).HasColumnName("IdMayDuc");
        });

        modelBuilder.Entity<MayDuc>(entity =>
        {
            entity.ToTable("MayDuc");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.TenMayDuc).HasMaxLength(255);
            entity.Property(e => e.NhaMay).HasColumnName("NhaMay");
            entity.Property(e => e.IsLock).HasColumnName("IsLock");
        });
         modelBuilder.Entity<NhomPhanLoaiMacThep>(entity =>
        {
            entity.ToTable("NhomPhanLoaiMacThep");
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TenNhom).HasColumnName("TenNhom");
        });

        // --- HRC1 ---
        modelBuilder.Entity<HRC1_MeThep>(entity =>
        {
            entity.ToTable("HRC1_MeThep", tb => tb.HasTrigger("trg_HRC1_MeThep_ChiPhi"));
            entity.Property(e => e.MaMe).HasMaxLength(30);
            entity.Property(e => e.ThungSo).HasMaxLength(20);
            entity.Property(e => e.ThoiGian).HasMaxLength(5);
            entity.Property(e => e.KLLFSauThep).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KlLan1).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KlLan2).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KlLan3).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KlThepLong).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KLThepLongPhanBo).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Kip).HasColumnType("nchar(1)");
            entity.Property(e => e.DichChuyen).HasMaxLength(20);
            entity.Property(e => e.GhiChuLo).HasMaxLength(500);
            entity.Property(e => e.PhanLoai).HasMaxLength(50);
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.MacThepBKMIS).HasMaxLength(50);
            entity.Property(e => e.GhiChuTL).HasMaxLength(500);
            entity.Property(e => e.GhiChuDuc).HasMaxLength(500);
            entity.Property(e => e.CapNhatLuc).HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()")
                  .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<HRC1_MePhanCong>(entity =>
        {
            entity.ToTable("HRC1_MePhanCong");
            entity.Property(e => e.IdPhieu).HasColumnName("IdPhieu");
            entity.Property(e => e.CongDoan).HasMaxLength(20);
            entity.Property(e => e.XacNhanLuc).HasColumnType("datetime");
        });

        modelBuilder.Entity<HRC1_LichSu>(entity =>
        {
            entity.ToTable("HRC1_LichSu");
            entity.Property(e => e.HanhDong).HasMaxLength(30);
            entity.Property(e => e.DuLieuCu).HasColumnType("nvarchar(MAX)");
            entity.Property(e => e.DuLieuMoi).HasColumnType("nvarchar(MAX)");
            entity.Property(e => e.Luc).HasColumnType("datetime");
        });

        modelBuilder.Entity<LG_NL_ChiTiet>(entity =>
        {
            entity.ToTable("LG_NL_ChiTiet");
            entity.Property(e => e.GiaTri).HasPrecision(18, 3);
            entity.Property(e => e.GiaTri_Goc).HasPrecision(18, 3);
            entity.Property(e => e.QuyKho).HasPrecision(18, 3);
        });
        modelBuilder.Entity<LG_PB_BienBanNhanQHLCCVH>(entity =>
        {
            entity.Property(e => e.KhoiLuongNhanVe).HasPrecision(18, 3);
            // Bảng có trigger DB — EF Core mặc định dùng OUTPUT clause để lấy giá trị sinh tự động,
            // nhưng SQL Server không cho phép OUTPUT trực tiếp trên bảng có trigger. Tắt để dùng
            // câu SELECT bù thay vì OUTPUT, tránh lỗi "target table has database triggers".
            entity.ToTable(tb => tb.UseSqlOutputClause(false));
        });
        modelBuilder.Entity<LG_PB_TyLePhanBo>(entity =>
        {
            entity.Property(e => e.TyLe).HasPrecision(9, 6);
            entity.ToTable(tb => tb.UseSqlOutputClause(false));
        });
        modelBuilder.Entity<LG_PB_TyLeNhom>(entity =>
        {
            entity.Property(e => e.TyLe).HasPrecision(9, 6);
            entity.ToTable(tb => tb.UseSqlOutputClause(false));
        });
        modelBuilder.Entity<LG_PB_KetQuaPhanBo>(entity =>
        {
            entity.Property(e => e.KhoiLuongNapLieu).HasPrecision(18, 3);
            entity.Property(e => e.TyLePhanBo).HasPrecision(9, 6);
            entity.Property(e => e.KhoiLuongPhanBo).HasPrecision(18, 3);
            entity.Property(e => e.KhoiLuongChotCuoi).HasPrecision(18, 3);
            entity.ToTable(tb => tb.UseSqlOutputClause(false));
        });
        modelBuilder.Entity<BkHrc2Slab>(entity =>
        {
            // HasTrigger: xem .claude/migrations/sanluong_phoitam_dq1_dq2.sql (trg_BkHrc2Slab_SanLuong) —
            // báo cho EF Core biết bảng có trigger để KHÔNG dùng OUTPUT clause khi INSERT/UPDATE.
            entity.ToTable("BK_HRC2_Slab", tb => tb.HasTrigger("trg_BkHrc2Slab_SanLuong"));

            entity.Property(e => e.BkmisId).HasColumnName("BkmisID");
            entity.Property(e => e.NgaySanXuat).HasColumnName("NgaySanXuat");
            entity.Property(e => e.NgaySXTheoCa).HasColumnName("NgaySXTheoCa");
            entity.Property(e => e.IdSlab).HasColumnName("IDSlab").HasMaxLength(100);
            entity.Property(e => e.ShiftName).HasMaxLength(50);
            entity.Property(e => e.CaSanXuat).HasColumnType("nchar(1)");
            entity.Property(e => e.KipSanXuat).HasColumnType("nchar(1)");
            entity.Property(e => e.MeThep).HasMaxLength(100);
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.ChatLuong).HasMaxLength(50);
            entity.Property(e => e.ChatLuongTPHH).HasMaxLength(50);
            entity.Property(e => e.ThongTinPhoi).HasMaxLength(200);
            entity.Property(e => e.TpKhongDatGangLong).HasColumnName("TPKhongDatGangLong").HasMaxLength(200);
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.LoaiPhoi).HasMaxLength(50);
            entity.Property(e => e.SapCode).HasColumnName("SAPCode").HasMaxLength(50);
            entity.Property(e => e.SapDescription).HasColumnName("SAPDescription").HasMaxLength(200);
            entity.Property(e => e.SoLo).HasMaxLength(100);
            entity.Property(e => e.OrderId).HasColumnName("OrderId").HasMaxLength(50);
            entity.Property(e => e.SapLastTime).HasColumnName("SAPLastTime");
            entity.Property(e => e.ChecksumVal).HasColumnName("Checksum_Val").HasMaxLength(50);
            entity.Property(e => e.ChieuDay).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChieuRong).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChieuDai).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.KhoiLuong).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.KhoiLuongTinhToan).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NgayTao).HasColumnType("datetime");
            entity.Property(e => e.ThoiDiemThaoTac).HasColumnType("datetime");
            entity.Property(e => e.IsChot).HasDefaultValue(false);
            entity.Property(e => e.MayDuc).HasColumnName("MayDuc");
            entity.Property(e => e.IsTrungIDSlab).HasColumnName("IsTrungIDSlab");
            entity.Property(e => e.IsDiffMacThep).HasColumnName("IsDiffMacThep");
            entity.Property(e => e.IsSaiLotName).HasColumnName("IsSaiLotName").HasDefaultValue(false);
            entity.Property(e => e.Line).HasColumnName("Line");
            entity.Property(e => e.PhanLoai).HasColumnName("PhanLoai").HasMaxLength(20);

            entity.HasOne(s => s.TrangThai)
                .WithOne(t => t.Slab)
                .HasForeignKey<BkHrc2SlabTrangThai>(t => t.IdSlab);
        });

        modelBuilder.Entity<BkHrc2SlabTrangThai>(entity =>
        {
            // HasTrigger: xem .claude/migrations/sanluong_phoitam_dq1_dq2.sql (trg_BkHrc2SlabTrangThai_SanLuong).
            entity.ToTable("BK_HRC2_Slab_TrangThai", tb => tb.HasTrigger("trg_BkHrc2SlabTrangThai_SanLuong"));
            entity.Property(e => e.NgayTao).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.NgayChuyenKCS).HasColumnType("datetime");
            entity.Property(e => e.NgayXacNhanDuc).HasColumnType("datetime");
            entity.Property(e => e.NgayXacNhanKho).HasColumnType("datetime");
            entity.Property(e => e.NgayChotPKH).HasColumnType("datetime");
            entity.Property(e => e.IdPhieuBBSL).HasColumnName("IdPhieuBBSL");
            entity.Property(e => e.NguoiChuyenKCS).HasColumnName("NguoiChuyenKCS");
            entity.Property(e => e.NguoiXacNhanDuc).HasColumnName("NguoiXacNhanDuc");
            entity.Property(e => e.NguoiXacNhanKho).HasColumnName("NguoiXacNhanKho");
            entity.Property(e => e.NguoiChotPKH).HasColumnName("NguoiChotPKH");
        });

        modelBuilder.Entity<BkSyncHrc2SlabControl>(entity =>
        {
            entity.ToTable("BK_SyncHRC2SlabControl");
            entity.Property(e => e.TableName).HasMaxLength(100);
            entity.Property(e => e.TrangThai).HasMaxLength(20);
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.BatDauLuc).HasColumnType("datetime");
            entity.Property(e => e.KetThucLuc).HasColumnType("datetime");
        });

        // Marker "đã check" độc lập theo user — không FK, chỉ liên kết theo quy ước tên cột.
        modelBuilder.Entity<BkHrc2Slab_UserCheck>(entity =>
        {
            entity.ToTable("BK_HRC2_Slab_UserCheck");
            entity.HasKey(e => new { e.IdUser, e.IdSlab });
            entity.Property(e => e.NgayCheck).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
        });

        // --- HRC1 Slab ---
        modelBuilder.Entity<Hrc1Slab>(entity =>
        {
            // HasTrigger: xem .claude/migrations/sanluong_phoitam_dq1_dq2.sql (trg_Hrc1Slab_SanLuong).
            entity.ToTable("HRC1_Slab", tb => tb.HasTrigger("trg_Hrc1Slab_SanLuong"));
            entity.Property(e => e.IDSlab).HasColumnName("IDSlab").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IDPiece).HasColumnName("IDPiece").HasMaxLength(50);
            entity.Property(e => e.MaMe).HasMaxLength(50);
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.CaSX).HasMaxLength(5);
            entity.Property(e => e.KipSX).HasMaxLength(5);
            entity.Property(e => e.MayDuc).HasMaxLength(10);
            entity.Property(e => e.CutDate).HasColumnType("datetime");
            entity.Property(e => e.ChieuDay).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ChieuRong).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ChieuDai).HasColumnType("decimal(10,2)");
            entity.Property(e => e.KhoiLuong).HasColumnType("decimal(10,2)");
            entity.Property(e => e.NgayTao)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()")
                  .ValueGeneratedOnAdd();
            entity.Property(e => e.NgayCapNhat).HasColumnType("datetime");
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.MaVatTu).HasMaxLength(100);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.NgayXoa).HasColumnType("datetime");

        });

        modelBuilder.Entity<MaVatTu>(entity =>
        {
            entity.ToTable("MaVatTu");
            entity.Property(e => e.NhaMay).HasMaxLength(10).IsRequired();
            entity.Property(e => e.MacThep).HasMaxLength(100);
            entity.Property(e => e.VatTuCode).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TenVatTu).HasMaxLength(200);
            entity.Property(e => e.CongDoan).HasMaxLength(100);
            entity.Property(e => e.KichThuoc).HasMaxLength(150);
            entity.Property(e => e.NgayTao)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()")
                  .ValueGeneratedOnAdd();
            // Unique chỉ trong nhóm cột thực sự có giá trị (tránh SQL Server coi nhiều NULL là trùng nhau):
            // - MacThep có giá trị: 1 Mác thép chỉ map 1 Vật tư/NhaMay (HRC1 Slab phụ thuộc điều này khi snapshot theo MacThep).
            // - CongDoan có giá trị: 1 Công đoạn + Vật tư chỉ tồn tại 1 lần/NhaMay.
            entity.HasIndex(e => new { e.NhaMay, e.MacThep })
                  .IsUnique()
                  .HasDatabaseName("UX_MaVatTu_NhaMay_MacThep")
                  .HasFilter("[MacThep] IS NOT NULL");
            entity.HasIndex(e => new { e.NhaMay, e.CongDoan, e.VatTuCode })
                  .IsUnique()
                  .HasDatabaseName("UX_MaVatTu_NhaMay_CongDoan_VatTuCode")
                  .HasFilter("[CongDoan] IS NOT NULL");
        });

        modelBuilder.Entity<Hrc1MaVatTu>(entity =>
        {
            entity.ToTable("HRC1_MaVatTu");
            entity.Property(e => e.MaVatTu).HasMaxLength(100).IsRequired();
            entity.Property(e => e.NgayTao)
                  .HasColumnType("datetime")
                  .HasDefaultValueSql("GETDATE()")
                  .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Hrc1BbslTongHopGhiChu>(entity =>
        {
            entity.ToTable("HRC1_BBSL_TongHop_GhiChu");
            entity.Property(e => e.MacThep).HasMaxLength(50);
            entity.Property(e => e.MaVatTu).HasMaxLength(100);
            entity.Property(e => e.GhiChu).HasMaxLength(500);
            entity.Property(e => e.NgayCapNhat).HasColumnType("datetime");
        });

        modelBuilder.Entity<Hrc1SlabTrangThai>(entity =>
        {
            // HasTrigger: xem .claude/migrations/sanluong_phoitam_dq1_dq2.sql (trg_Hrc1SlabTrangThai_SanLuong_ChuyenCa).
            entity.ToTable("HRC1_Slab_TrangThai", tb => tb.HasTrigger("trg_Hrc1SlabTrangThai_SanLuong_ChuyenCa"));
            entity.HasIndex(e => e.IdSlab).IsUnique();
            entity.Property(e => e.NgayTao).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.NgayChuyen).HasColumnType("datetime");
            entity.Property(e => e.NgayXacNhanDuc).HasColumnType("datetime");
            entity.Property(e => e.NgayXacNhanCan).HasColumnType("datetime");
            entity.Property(e => e.NgayXacNhanC4).HasColumnType("datetime");
            entity.Property(e => e.NgayChotPKH).HasColumnType("datetime");
        });

        // Bảng phụ lưu IDSlab đã sửa tay — xem comment trên Hrc1SlabEdit.
        modelBuilder.Entity<Hrc1SlabEdit>(entity =>
        {
            entity.ToTable("HRC1_Slab_Edit");
            entity.HasIndex(e => e.IdSlab).IsUnique();
            // Cột DB đặt tên IDSlabMoi (khác IdSlab) vì SQL Server mặc định collation không phân
            // biệt hoa/thường — "IdSlab" và "IDSlab" bị coi là trùng tên cột (xem hrc1_slab_edit.sql).
            entity.Property(e => e.IDSlab).HasColumnName("IDSlabMoi").HasMaxLength(50).IsRequired();
            entity.Property(e => e.NgayCapNhat).HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

}
