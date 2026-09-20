using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pf2e.Application.Features.Accounts;
using Pf2e.Domain.Accounts;

namespace Pf2e.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> accounts)
    {
        accounts.ToTable("Accounts");
        accounts.HasKey(a => a.Id);

        // Every id in this schema is made up by a handler, never by the store. See
        // TrackedCharacterConfiguration for what EF does with a set key and no such instruction.
        accounts.Property(a => a.Id).ValueGeneratedNever();

        accounts.Property(a => a.Email).HasMaxLength(AccountCredentials.MaxEmail).IsRequired();

        // The handler lower-cases and trims before it writes, and this is what makes that
        // discipline hold: two spellings of one address become impossible rather than unlikely,
        // including against a registration racing another on the same address.
        accounts.HasIndex(a => a.Email).IsUnique();

        accounts.Property(a => a.DisplayName).HasMaxLength(AccountCredentials.MaxDisplayName).IsRequired();

        // Long enough for the current format with room to spare, so raising the work factor
        // later does not need a migration.
        accounts.Property(a => a.PasswordHash).HasMaxLength(256).IsRequired();
    }
}
