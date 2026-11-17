using Microsoft.EntityFrameworkCore;
using Nexus.Web.Models;

namespace Nexus.Web.Data;

/// <summary>EF Core context for the whole system (SQL Server 2022).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<City> Cities => Set<City>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RetailShop> RetailShops => Set<RetailShop>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<SecurityDepositRate> DepositRates => Set<SecurityDepositRate>();
    public DbSet<DiscountScheme> DiscountSchemes => Set<DiscountScheme>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<FeasibilityCheck> FeasibilityChecks => Set<FeasibilityCheck>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<ConnectionEquipment> ConnectionEquipment => Set<ConnectionEquipment>();
    public DbSet<ConnectionEvent> ConnectionEvents => Set<ConnectionEvent>();
    public DbSet<ConnectionCharge> ConnectionCharges => Set<ConnectionCharge>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<OrderDocument> OrderDocuments => Set<OrderDocument>();
    public DbSet<SequenceCounter> Sequences => Set<SequenceCounter>();

    // Money columns share one precision.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // No cascading deletes anywhere: history must never vanish with a parent row.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        // Unique business keys.
        b.Entity<City>().HasIndex(x => x.Code).IsUnique();
        b.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.UserId).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.Phone);
        b.Entity<ServiceOrder>().HasIndex(x => x.OrderNo).IsUnique();
        b.Entity<Connection>().HasIndex(x => x.AccountId).IsUnique();
        b.Entity<Bill>().HasIndex(x => x.BillNo).IsUnique();
        b.Entity<Payment>().HasIndex(x => x.ReceiptNo).IsUnique();
        b.Entity<PurchaseOrder>().HasIndex(x => x.PoNumber).IsUnique();
        b.Entity<SecurityDepositRate>().HasIndex(x => x.ConnectionType).IsUnique();

        // Searchable columns.
        b.Entity<ServiceOrder>().HasIndex(x => x.PlacedOn);
        b.Entity<Connection>().HasIndex(x => x.ActivatedOn);

        // Rates (per minute) need four decimals.
        b.Entity<Plan>().Property(x => x.LocalRate).HasPrecision(18, 4);
        b.Entity<Plan>().Property(x => x.StdRate).HasPrecision(18, 4);
        b.Entity<Plan>().Property(x => x.MobileMsgRate).HasPrecision(18, 4);
        b.Entity<FeasibilityCheck>().Property(x => x.DistanceKm).HasPrecision(8, 2);
        b.Entity<DiscountScheme>().Property(x => x.Percent).HasPrecision(5, 2);
        b.Entity<ServiceOrder>().Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.Entity<Connection>().Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.Entity<Bill>().Property(x => x.TaxPercent).HasPrecision(5, 2);
        b.Entity<Bill>().Property(x => x.LocalMinutes).HasPrecision(12, 2);
        b.Entity<Bill>().Property(x => x.StdMinutes).HasPrecision(12, 2);
        b.Entity<Bill>().Property(x => x.MobileMsgMinutes).HasPrecision(12, 2);

        // Two relationships from Customer/User need explicit wiring.
        b.Entity<Customer>().HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
        b.Entity<Employee>().HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        b.Entity<ServiceOrder>().HasOne(o => o.Plan).WithMany().HasForeignKey(o => o.PlanId);
        b.Entity<ServiceOrder>().HasOne(o => o.LandlinePlan).WithMany().HasForeignKey(o => o.LandlinePlanId);
        b.Entity<ServiceOrder>().HasOne(o => o.InstallCity).WithMany().HasForeignKey(o => o.InstallCityId);
    }
}
