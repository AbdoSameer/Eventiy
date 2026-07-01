using Domain.Aggregates.UserAggregate;
using Domain.Aggregates.UserAggregate.ValueObject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration
{
    internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Id)
                   .HasConversion(id => id.Value, guid => UserId.Create(guid).Value);

            builder.OwnsOne(u => u.Email, emailBuilder =>
            {
                emailBuilder.Property(e => e.Value)
                            .HasColumnName("Email")
                            .HasMaxLength(256)
                            .IsRequired();

                emailBuilder.HasIndex(e => e.Value).IsUnique();
            });

            builder.OwnsOne(u => u.Role, roleBuilder =>
            {
                roleBuilder.Property(r => r.Value)
                           .HasColumnName("Role")
                           .HasMaxLength(50)
                           .IsRequired();
            });

            builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();
        }
    }

}
