using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Infrastructure.Persistence.Configurations;

internal sealed class PractitionerConfiguration : IEntityTypeConfiguration<Practitioner>
{
    public void Configure(EntityTypeBuilder<Practitioner> builder)
    {
        builder.ToTable("practitioners");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Speciality).HasColumnName("speciality").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PlaceOfWork).HasColumnName("place_of_work").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(255);

        builder.HasIndex(x => x.SourceEventId).IsUnique().HasFilter("source_event_id IS NOT NULL").HasDatabaseName("ux_practitioners_source_event_id");
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_practitioners_email_active");
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_practitioners_phone_active");
        builder.HasIndex(x => x.LastName).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_last_name_trgm");
        builder.HasIndex(x => x.FirstName).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_first_name_trgm");
        builder.HasIndex(x => x.Speciality).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_speciality_trgm");
        builder.HasIndex(x => x.PlaceOfWork).HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_place_of_work_trgm");
    }
}