using Microsoft.EntityFrameworkCore;
using Qatra.Api.Models;

namespace Qatra.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<WaterSupplyEvent> WaterSupplyEvents => Set<WaterSupplyEvent>();
    public DbSet<Prediction> Predictions => Set<Prediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Neighborhoods ----------
        modelBuilder.Entity<Neighborhood>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ---------- Devices ----------
        modelBuilder.Entity<Device>(e =>
        {
            // SHA256 بصيغة hex = 64 حرفاً
            e.Property(x => x.DeviceKeyHash).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.DeviceKeyHash).IsUnique();
        });

        // ---------- Reports ----------
        modelBuilder.Entity<Report>(e =>
        {
            e.Property(x => x.RawText).IsRequired().HasMaxLength(1000);

            // تخزين الـ Enums كنصوص مقروءة في قاعدة البيانات
            e.Property(x => x.QuickButton).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IssueType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HumanLabel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(20);

            // Restrict لتجنب خطأ "multiple cascade paths" في SQL Server
            e.HasOne(x => x.Device).WithMany(d => d.Reports)
                .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Neighborhood).WithMany(n => n.Reports)
                .HasForeignKey(x => x.NeighborhoodId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Incident).WithMany(i => i.Reports)
                .HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);

            // العامل الخلفي: البلاغات Pending لكل حي
            e.HasIndex(x => new { x.NeighborhoodId, x.ProcessingStatus });
            // الحد اليومي لكل جهاز
            e.HasIndex(x => new { x.DeviceId, x.CreatedAt });
        });

        // ---------- Incidents ----------
        modelBuilder.Entity<Incident>(e =>
        {
            e.Property(x => x.IssueType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Explanation).HasMaxLength(1000);

            e.HasOne(x => x.Neighborhood).WithMany(n => n.Incidents)
                .HasForeignKey(x => x.NeighborhoodId).OnDelete(DeleteBehavior.Restrict);

            // البحث عن حالة مفتوحة عند الدمج + إغلاق الحالات الخاملة
            e.HasIndex(x => new { x.NeighborhoodId, x.Status, x.LastReportAt });
        });

        // ---------- WaterSupplyEvents ----------
        modelBuilder.Entity<WaterSupplyEvent>(e =>
        {
            e.HasOne(x => x.Neighborhood).WithMany(n => n.WaterSupplyEvents)
                .HasForeignKey(x => x.NeighborhoodId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.NeighborhoodId, x.EventDate });
        });

        // ---------- Predictions ----------
        modelBuilder.Entity<Prediction>(e =>
        {
            e.Property(x => x.ModelVersion).IsRequired().HasMaxLength(50);
            e.Property(x => x.Inputs).IsRequired();
            e.Property(x => x.Outputs).IsRequired();

            e.HasOne(x => x.Neighborhood).WithMany(n => n.Predictions)
                .HasForeignKey(x => x.NeighborhoodId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.NeighborhoodId, x.CreatedAt });
        });

        modelBuilder.Entity<Neighborhood>().HasData(
            new Neighborhood { Id = 1, Name = "الديس", PriorCycleDays = 4, PriorCycleSd = 1.0 },
            new Neighborhood { Id = 2, Name = "فوه", PriorCycleDays = 5, PriorCycleSd = 1.2 },
            new Neighborhood { Id = 3, Name = "الشرج", PriorCycleDays = 7, PriorCycleSd = 1.5 },
            new Neighborhood { Id = 4, Name = "روكب", PriorCycleDays = 9, PriorCycleSd = 2.0 },
            new Neighborhood { Id = 5, Name = "السلام", PriorCycleDays = 6, PriorCycleSd = 1.5 }
        );
    }
}