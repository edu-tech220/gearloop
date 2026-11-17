using GearLoop.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Data;

public class GearLoopDbContext : DbContext
{
    public GearLoopDbContext(DbContextOptions<GearLoopDbContext> options) : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<EquipmentModel> EquipmentModels => Set<EquipmentModel>();
    public DbSet<EquipmentUnit> EquipmentUnits => Set<EquipmentUnit>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<RentalCharge> RentalCharges => Set<RentalCharge>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.City).HasMaxLength(120).IsRequired();
            b.Property(x => x.Address).HasMaxLength(250).IsRequired();
        });

        modelBuilder.Entity<EquipmentModel>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DailyRate).HasColumnType("decimal(10,2)");
            e.Property(x => x.DepositAmount).HasColumnType("decimal(10,2)");
            e.Property(x => x.ReplacementValue).HasColumnType("decimal(10,2)");
            e.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<EquipmentUnit>(u =>
        {
            u.Property(x => x.SerialNumber).HasMaxLength(60).IsRequired();
            u.HasIndex(x => x.SerialNumber).IsUnique();

            // App-managed concurrency token (not a database-generated rowversion) so the same
            // model and application code behave identically on SQL Server and the SQLite
            // provider used by the test suite.
            u.Property(x => x.RowVersion).IsConcurrencyToken();

            u.HasOne(x => x.EquipmentModel)
                .WithMany(m => m.Units)
                .HasForeignKey(x => x.EquipmentModelId)
                .OnDelete(DeleteBehavior.Restrict);

            u.HasOne(x => x.Branch)
                .WithMany(br => br.Units)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            u.HasIndex(x => new { x.EquipmentModelId, x.BranchId, x.Status });
        });

        modelBuilder.Entity<AppUser>(u =>
        {
            u.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            u.Property(x => x.Email).HasMaxLength(180).IsRequired();
            u.Property(x => x.PasswordHash).IsRequired();
            u.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Reservation>(r =>
        {
            r.Property(x => x.RowVersion).IsConcurrencyToken();

            r.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasOne(x => x.EquipmentModel)
                .WithMany()
                .HasForeignKey(x => x.EquipmentModelId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasOne(x => x.AssignedUnit)
                .WithMany()
                .HasForeignKey(x => x.AssignedUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasIndex(x => new { x.EquipmentModelId, x.BranchId, x.Status, x.StartDate, x.EndDate });
            r.HasIndex(x => x.CustomerId);
        });

        modelBuilder.Entity<RentalCharge>(c =>
        {
            c.Property(x => x.BaseAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.DiscountAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.SurchargeAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.LateFeeAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.DamageFeeAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.DepositAmount).HasColumnType("decimal(10,2)");
            c.Property(x => x.DepositRefunded).HasColumnType("decimal(10,2)");
            c.Property(x => x.TotalAmount).HasColumnType("decimal(10,2)");

            c.HasOne(x => x.Reservation)
                .WithOne(x => x.Charge)
                .HasForeignKey<RentalCharge>(x => x.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            c.HasIndex(x => x.ReservationId).IsUnique();
        });

        modelBuilder.Entity<WaitlistEntry>(w =>
        {
            w.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            w.HasOne(x => x.EquipmentModel)
                .WithMany()
                .HasForeignKey(x => x.EquipmentModelId)
                .OnDelete(DeleteBehavior.Restrict);

            w.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            w.HasIndex(x => new { x.EquipmentModelId, x.BranchId, x.Status, x.CreatedAtUtc });
        });
    }
}
