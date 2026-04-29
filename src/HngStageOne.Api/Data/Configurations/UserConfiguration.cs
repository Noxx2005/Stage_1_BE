using HngStageOne.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HngStageOne.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.GitHubUsername).IsRequired().HasMaxLength(100);
        builder.Property(user => user.Email).HasMaxLength(255);
        builder.Property(user => user.AvatarUrl).HasMaxLength(500);
        builder.Property(user => user.Role).IsRequired().HasMaxLength(20);
        builder.Property(user => user.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(user => user.GitHubId).IsUnique();
        builder.HasIndex(user => user.GitHubUsername);
    }
}
