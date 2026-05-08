using TruckBor.Domain.Entities;

namespace TruckBor.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Post> Posts { get; }
    IRepository<Payment> Payments { get; }
    IRepository<Subscription> Subscriptions { get; }
    IRepository<Tariff> Tariffs { get; }
    IRepository<TelegramAccount> TelegramAccounts { get; }
    IRepository<Group> Groups { get; }
    IRepository<Card> Cards { get; }
    IRepository<Channel> Channels { get; }
    IRepository<Setting> Settings { get; }
    IRepository<AdminUser> AdminUsers { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}