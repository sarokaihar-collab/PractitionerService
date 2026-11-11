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
        
        // Phone must be unique among active (non-deleted) practitioners.
        builder.HasIndex(x => x.Phone).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_practitioners_phone_active"); 
        builder.Property(x => x.Speciality).HasColumnName("speciality").HasMaxLength(200).IsRequired();
        builder.Property(x => x.PlaceOfWork).HasColumnName("place_of_work").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.SourceEventId).HasColumnName("source_event_id").HasMaxLength(100);

        // SourceEventId must be unique among non-null values (partial unique index).
        builder.HasIndex(x => x.SourceEventId).IsUnique().HasFilter("source_event_id IS NOT NULL").HasDatabaseName("ux_practitioners_source_event_id");

        // Email must be unique among active (non-deleted) practitioners.
        // Soft-deleted records do not block creating a new one with the same email.
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("is_deleted = false").HasDatabaseName("ux_practitioners_email_active");

        // Search indexes. Partial indexes (WHERE is_deleted = false) speed up typical queries.
        builder.HasIndex(x => x.LastName).HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_last_name_active");
        builder.HasIndex(x => x.FirstName).HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_first_name_active");
        builder.HasIndex(x => x.Speciality).HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_speciality_active");
        builder.HasIndex(x => x.PlaceOfWork).HasFilter("is_deleted = false").HasDatabaseName("ix_practitioners_place_of_work_active");
    }
}