using Microsoft.EntityFrameworkCore;
using TruckBor.Application.Interfaces;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<TelegramAccount> TelegramAccounts => Set<TelegramAccount>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    // New entities
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();
    public DbSet<VirtualNumberOrder> VirtualNumberOrders => Set<VirtualNumberOrder>();
    public DbSet<PremiumOrder> PremiumOrders => Set<PremiumOrder>();
    public DbSet<VideoTutorial> VideoTutorials => Set<VideoTutorial>();
    public DbSet<ScrapedPost> ScrapedPosts => Set<ScrapedPost>();
    public DbSet<SystemAccount> SystemAccounts => Set<SystemAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // BalanceTransaction indexes
        modelBuilder.Entity<BalanceTransaction>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });

        // VirtualNumberOrder indexes
        modelBuilder.Entity<VirtualNumberOrder>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ActivationId);
        });

        // PremiumOrder indexes
        modelBuilder.Entity<PremiumOrder>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Status);
        });

        // ScrapedPost indexes
        modelBuilder.Entity<ScrapedPost>(e =>
        {
            e.HasIndex(x => new { x.SourceGroupId, x.TelegramMessageId }).IsUnique();
            e.HasIndex(x => x.FromCity);
            e.HasIndex(x => x.ToCity);
            e.HasIndex(x => x.IsRelevant);
            e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => x.MessageDate);
        });

        // SystemAccount indexes
        modelBuilder.Entity<SystemAccount>(e =>
        {
            e.HasIndex(x => x.PhoneNumber).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<TruckBor.Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
