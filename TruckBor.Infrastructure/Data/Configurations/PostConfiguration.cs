using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromCity).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ToCity).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CargoType).HasMaxLength(200);
        builder.Property(x => x.Weight).HasMaxLength(50);
        builder.Property(x => x.Price).HasMaxLength(100);
        builder.Property(x => x.ContactPhone).HasMaxLength(20);
        builder.Property(x => x.GroupSource).HasMaxLength(200);
    }
}