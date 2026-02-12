// Data/ForrajeriaContext.cs
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ForrajeriaJovitaAPI.Models;

namespace ForrajeriaJovitaAPI.Data
{
    public class ForrajeriaContext : DbContext
    {
        public ForrajeriaContext(DbContextOptions<ForrajeriaContext> options) : base(options) { }

        // DbSets
        public DbSet<Access> Accesses { get; set; } = null!;
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<CashMovement> CashMovements { get; set; } = null!;
        public DbSet<CashSession> CashSessions { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Configuration> Configurations { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<ProductSeason> ProductsSeasons { get; set; } = null!;
        public DbSet<ProductStock> ProductsStocks { get; set; } = null!;
        public DbSet<ProductUnit> ProductUnits { get; set; } = null!;
        public DbSet<ProductUnitPrice> ProductUnitPrices { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RoleAccess> RolesAccesses { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleItem> SalesItems { get; set; } = null!;
        public DbSet<SaleItemToReturn> SaleItemToReturn { get; set; } = null!;
        public DbSet<SalePayment> SalesPayments { get; set; } = null!;
        public DbSet<Season> Seasons { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;
        // PaymentTransaction
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de precisión para decimales
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(18);
                property.SetScale(2);
            }

            // Access
            modelBuilder.Entity<Access>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired();
                entity.Property(e => e.Description).IsRequired();
            });

            // Branch
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Address).IsRequired();
            });

            // CashMovement
            modelBuilder.Entity<CashMovement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasConversion<int>();

                entity.HasOne(e => e.CashSession)
                      .WithMany(c => c.CashMovements)
                      .HasForeignKey(e => e.CashSessionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // CashSession
            modelBuilder.Entity<CashSession>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Branch)
                      .WithMany(b => b.CashSessions)
                      .HasForeignKey(e => e.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.OpenedByUser)
                      .WithMany(u => u.OpenedCashSessions)
                      .HasForeignKey(e => e.OpenedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ClosedByUser)
                      .WithMany(u => u.ClosedCashSessions)
                      .HasForeignKey(e => e.ClosedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Client
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired();
                entity.Property(e => e.Phone).IsRequired();
                entity.Property(e => e.Document).IsRequired();
            });

            // Configuration
            modelBuilder.Entity<Configuration>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).IsRequired();
            });

            // Product
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ProductSeason
            modelBuilder.Entity<ProductSeason>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.ProductsSeasons)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Season)
                      .WithMany(s => s.ProductsSeasons)
                      .HasForeignKey(e => e.SeasonId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ProductStock
            modelBuilder.Entity<ProductStock>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.ProductsStocks)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                      .WithMany(b => b.ProductsStocks)
                      .HasForeignKey(e => e.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ProductUnit
            modelBuilder.Entity<ProductUnit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DisplayName).IsRequired();
                entity.Property(e => e.UnitLabel).IsRequired();
                entity.Property(e => e.StockRounding).HasConversion<int>();

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.ProductUnits)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ProductUnitPrice
            modelBuilder.Entity<ProductUnitPrice>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Tier).HasConversion<int>();

                entity.HasOne(e => e.ProductUnit)
                      .WithMany(pu => pu.ProductUnitPrices)
                      .HasForeignKey(e => e.ProductUnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Promotion
            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.Promotions)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
            });

            // RoleAccess
            modelBuilder.Entity<RoleAccess>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Access)
                      .WithMany(a => a.RolesAccesses)
                      .HasForeignKey(e => e.AccessId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Role)
                      .WithMany(r => r.RolesAccesses)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Sale
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.CashMovement)
                      .WithMany(cm => cm.Sales)
                      .HasForeignKey(e => e.CashMovementId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SellerUser)
                      .WithMany(u => u.Sales)
                      .HasForeignKey(e => e.SellerUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SaleItem
            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Sale)
                      .WithMany(s => s.SalesItems)
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.SalesItems)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ProductUnit)
                      .WithMany()
                      .HasForeignKey(e => e.ProductUnitId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SaleItemToReturn
            modelBuilder.Entity<SaleItemToReturn>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            // SalePayment
            modelBuilder.Entity<SalePayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Method).HasConversion<int>();

                entity.HasOne(e => e.Sale)
                      .WithMany(s => s.SalesPayments)
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Season
            modelBuilder.Entity<Season>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
            });

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.LastName).IsRequired();
                entity.Property(e => e.UserName).IsRequired();
                entity.Property(e => e.Password).IsRequired();

                entity.HasOne(e => e.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PaymentTransaction
            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Índice único en TransactionId (no puede haber duplicados)
                entity.HasIndex(e => e.TransactionId).IsUnique();

                // Índice en CheckoutId para búsquedas rápidas
                entity.HasIndex(e => e.CheckoutId);

                // Índice en SaleId para búsquedas rápidas
                entity.HasIndex(e => e.SaleId);

                // Relación con Sale (una venta puede tener múltiples intentos de pago)
                entity.HasOne(e => e.Sale)
                      .WithMany()
                      .HasForeignKey(e => e.SaleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Setting
            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Value).IsRequired().HasColumnType("nvarchar(max)");
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            });
        }
    }
}