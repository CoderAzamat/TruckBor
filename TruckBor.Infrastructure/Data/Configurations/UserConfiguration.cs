using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TelegramId).IsRequired();
        builder.HasIndex(x => x.TelegramId).IsUnique();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.Balance).HasPrecision(18, 2);

        builder.HasMany(x => x.Posts).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        builder.HasMany(x => x.Payments).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        builder.HasMany(x => x.Subscriptions).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        builder.HasMany(x => x.TelegramAccounts).WithOne(x => x.User).HasForeignKey(x => x.UserId);
    }
}