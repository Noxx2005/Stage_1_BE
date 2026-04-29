using HngStageOne.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HngStageOne.Api.Data.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255)
            .UseCollation("NOCASE");

        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.Property(p => p.Gender)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.GenderProbability)
            .HasColumnType("REAL");

        builder.Property(p => p.Age)
            .IsRequired();

        builder.Property(p => p.AgeGroup)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CountryId)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(p => p.CountryName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.CountryProbability)
            .HasColumnType("REAL");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnType("TEXT");

        builder.HasIndex(p => p.Gender);
        builder.HasIndex(p => p.AgeGroup);
        builder.HasIndex(p => p.CountryId);
        builder.HasIndex(p => p.Age);
        builder.HasIndex(p => p.CreatedAt);
    }
}
