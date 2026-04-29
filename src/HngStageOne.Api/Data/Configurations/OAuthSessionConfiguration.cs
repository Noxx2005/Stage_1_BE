using HngStageOne.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HngStageOne.Api.Data.Configurations;

public class OAuthSessionConfiguration : IEntityTypeConfiguration<OAuthSession>
{
    public void Configure(EntityTypeBuilder<OAuthSession> builder)
    {
        builder.ToTable("OAuthSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.State).IsRequired().HasMaxLength(128);
        builder.Property(session => session.CodeVerifier).IsRequired().HasMaxLength(256);
        builder.Property(session => session.RedirectUri).IsRequired().HasMaxLength(500);
        builder.Property(session => session.ClientType).IsRequired().HasMaxLength(20);
        builder.HasIndex(session => session.State).IsUnique();
    }
}
