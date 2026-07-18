using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.MaxScore)
            .HasPrecision(5, 2);

        builder.Property(a => a.Weight)
            .HasPrecision(5, 2);

        builder.Property(a => a.CourseId)
            .IsRequired();

        builder.HasOne(a => a.Course)
            .WithMany()
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
