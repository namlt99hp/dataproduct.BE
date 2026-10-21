using Data_Product.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace dataproduct.api.Models.MasterData;

public partial class ProductDataMasterDbContext : DbContext
{
    public ProductDataMasterDbContext()
    {
    }

    public ProductDataMasterDbContext(DbContextOptions<ProductDataMasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaiKhoan> Tbl_TaiKhoan { get; set; }
    public DbSet<PhongBan> Tbl_PhongBan { get; set; }
    public DbSet<ViTri> Tbl_ViTri { get; set; }
    public DbSet<Tbl_Kip> Tbl_Kip { get; set; }
    public DbSet<Tbl_LoCao> Tbl_LoCao { get; set; }
    public DbSet<TonSiLoLoCaResult> TonSiLoLoCaResults { get; set; }
   
    public DbSet<Tbl_MeThoi> Tbl_MeThoi { get; set; }
    public DbSet<Tbl_BM_16_LoSanXuat> Tbl_BM_16_LoSanXuat { get; set; }
    public DbSet<Tbl_BM_16_LoSanXuat_TaiKhoan> Tbl_BM_16_LoSanXuat_TaiKhoan { get; set; }
    public DbSet<Tbl_BienBanGiaoNhan> Tbl_BienBanGiaoNhan { get; set; }
    public DbSet<Tbl_ChiTiet_BienBanGiaoNhan> Tbl_ChiTiet_BienBanGiaoNhan { get; set; }
    public DbSet<Tbl_Xuong> Tbl_Xuong { get; set; }
    public DbSet<TongNhanVeBbgnResult> TongNhanVeBbgnResults { get; set; }
    public DbSet<Tbl_VatTu> Tbl_VatTu { get; set; }
    public DbSet<Tbl_MaLo> Tbl_MaLo { get; set; }
    public DbSet<Tbl_VatTuMaLo> Tbl_VatTuMaLo { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<TaiKhoan>(entity =>
        //{
        //    entity.ToTable("Tbl_TaiKhoan");

        //    entity.Property(e => e.NgayTao)
        //          .HasColumnType("date");

        //    entity.Property(e => e.ChuKy)
        //          .HasColumnType("nvarchar(MAX)");

        //    entity.Property(e => e.PhongBan_Them)
        //          .HasColumnType("nvarchar(MAX)");
        //});
        modelBuilder.Entity<TaiKhoan>()
            .HasOne(t => t.PhongBan)
            .WithMany(p => p.TaiKhoans)
            .HasForeignKey(t => t.ID_PhongBan);
            

        modelBuilder.Entity<TonSiLoLoCaResult>().HasNoKey().ToView(null);
        modelBuilder.Entity<TongNhanVeBbgnResult>().HasNoKey().ToView(null);

        // Cấu hình entity keyless cho stored procedure results
        modelBuilder.Entity<StoredProcedureScalarResult>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null); // Không map đến view/table nào
            // Map property Result với bất kỳ tên cột nào từ stored procedure
            entity.Property(e => e.Result)
                .HasColumnName("Result"); // Có thể là "Result", "TotalKL", hoặc tên cột khác
        });
        modelBuilder.Entity<LG_NKVHPT_ChiTiet>(entity =>
        {
            entity.ToTable("LG_NKVHPT_ChiTiet",
                tb => tb.HasTrigger("trg_LG_NKVHPT_ChiTiet_Sync_ThanPCI"));
        });
        base.OnModelCreating(modelBuilder);
    }
}
