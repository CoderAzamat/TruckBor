using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckBor.Domain.Entities;

namespace TruckBor.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CheckFileId).HasMaxLength(200);
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
    }
}