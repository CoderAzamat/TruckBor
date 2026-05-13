using Microsoft.EntityFrameworkCore;
using TruckBor.Domain.Entities;

namespace TruckBor.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Post> Posts { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Tariff> Tariffs { get; }
    DbSet<TelegramAccount> TelegramAccounts { get; }
    DbSet<Group> Groups { get; }
    DbSet<Card> Cards { get; }
    DbSet<Channel> Channels { get; }
    DbSet<Setting> Settings { get; }
    DbSet<AdminUser> AdminUsers { get; }

    // New entities
    DbSet<PaymentProvider> PaymentProviders { get; }
    DbSet<BalanceTransaction> BalanceTransactions { get; }
    DbSet<VirtualNumberOrder> VirtualNumberOrders { get; }
    DbSet<PremiumOrder> PremiumOrders { get; }
    DbSet<VideoTutorial> VideoTutorials { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
