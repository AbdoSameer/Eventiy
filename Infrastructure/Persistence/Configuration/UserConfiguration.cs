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

            builder.Property(u => u.FirstName)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(u => u.LastName)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.OwnsOne(u => u.Email, emailBuilder =>
            {
                emailBuilder.Property(e => e.Value)
                            .HasColumnName("Email")
                            .HasMaxLength(256)
                            .IsRequired();

                emailBuilder.HasIndex(e => e.Value).IsUnique();
            });

            builder.Property(u => u.Role)
                   .HasColumnName("Role")
                   .HasConversion(
                       role => role.Value,
                       value => Role.FromString(value).Value)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();

           builder.Property(u => u.FirstName)
                   .HasColumnName("FirstName")
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(u => u.LastName)
                   .HasColumnName("LastName")
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(u => u.IsApproved)
                   .HasDefaultValue(true)
                   .IsRequired();

            builder.Property<byte[]>("RowVersion").IsRowVersion();
        }
    }

}
