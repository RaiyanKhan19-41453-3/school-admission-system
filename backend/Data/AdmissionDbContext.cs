using Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Data;

public class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<AcademicYear>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.IsActive);   // one active year (enforced in service)
            e.Property(x => x.Name).HasMaxLength(50);
            e.HasMany(x => x.Classes).WithOne(c => c.AcademicYear)
             .HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<SchoolClass>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AcademicYearId, x.Name }).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.FeeAmount).HasPrecision(18, 2);
            e.HasMany(x => x.Holds).WithOne(h => h.Class)
             .HasForeignKey(h => h.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Nid).IsUnique();
            e.Property(x => x.Nid).HasMaxLength(17);
            e.Property(x => x.Role).HasMaxLength(20);
            e.HasMany(x => x.Holds).WithOne(h => h.Student)
             .HasForeignKey(h => h.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Hold>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt });  // capacity + expiry lookups
            e.HasIndex(x => new { x.StudentId, x.Status });             // "my applications"
            e.Property(x => x.Status).HasMaxLength(16);
            e.HasOne(x => x.Payment).WithOne(p => p.Hold)
             .HasForeignKey<Payment>(p => p.HoldId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.HoldId).IsUnique();   // one payment per application
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasMaxLength(16);
        });
    }
}
