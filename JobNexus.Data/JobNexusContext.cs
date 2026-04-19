using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using JobNexus.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace JobNexus.Data;

public partial class JobNexusContext : IdentityDbContext<Profile>
{
    public JobNexusContext()
    {
    }

    public JobNexusContext(DbContextOptions<JobNexusContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Certification> Certifications { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<JobSource> JobSources { get; set; }

    public virtual DbSet<Profile> Profiles { get; set; }

    public virtual DbSet<Resume> Resumes { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<AppliedJob> AppliedJobs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Certification>(entity =>
        {
            entity.HasKey(e => e.CertId).HasName("PRIMARY");

            entity.ToTable("Certification");

            entity.HasIndex(e => e.Username, "username");

            entity.Property(e => e.CertId).HasColumnName("cert_id");
            entity.Property(e => e.CertName)
                .HasMaxLength(50)
                .HasColumnName("cert_name");
            entity.Property(e => e.DateGiven)
                .HasColumnType("datetime")
                .HasColumnName("date_given");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");

            entity.HasOne(d => d.Profile).WithMany(p => p.Certifications)
                .HasForeignKey(d => d.Username)
                .HasConstraintName("Certification_ibfk_1");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId).HasName("PRIMARY");

            entity.ToTable("Company");

            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(100)
                .HasColumnName("company_name");
            entity.Property(e => e.Industry).HasMaxLength(100);
            entity.Property(e => e.WebsiteUrl)
                .HasMaxLength(250)
                .HasColumnName("Website_url");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.JobId).HasName("PRIMARY");

            entity.ToTable("Job");

            entity.HasIndex(e => e.CompanyId, "company_id");

            entity.HasIndex(e => e.SourceId, "source_id");

            entity.Property(e => e.JobId).HasColumnName("Job_id");
            entity.Property(e => e.ApplyUrl)
                .HasMaxLength(200)
                .HasColumnName("apply_url");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.DatePosted)
                .HasColumnType("datetime")
                .HasColumnName("date_posted");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.Pay).HasColumnName("pay");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .HasColumnName("title");

            entity.HasOne(d => d.Company).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.CompanyId)
                .HasConstraintName("Job_ibfk_2");

            entity.HasOne(d => d.Source).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.SourceId)
                .HasConstraintName("Job_ibfk_1");
        });

        modelBuilder.Entity<JobSource>(entity =>
        {
            entity.HasKey(e => e.SourceId).HasName("PRIMARY");

            entity.ToTable("JobSource");

            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceName)
                .HasMaxLength(150)
                .HasColumnName("source_name");
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("Profile");
            entity.Property(e => e.Bio)
                .HasMaxLength(200)
                .HasColumnName("bio");
            entity.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");

            entity.HasMany(d => d.Jobs).WithMany(p => p.Usernames)
                .UsingEntity<Dictionary<string, object>>(
                    "Applied",
                    r => r.HasOne<Job>().WithMany()
                        .HasForeignKey("JobId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Applied_ibfk_2"),
                    l => l.HasOne<Profile>().WithMany()
                        .HasForeignKey("Username")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Applied_ibfk_1"),
                    j =>
                    {
                        j.HasKey("Username", "JobId");
                        j.ToTable("Applied");
                        j.HasIndex(new[] { "JobId" }, "job_id");
                        j.IndexerProperty<string>("Username")
                            .HasMaxLength(50)
                            .HasColumnName("username");
                        j.IndexerProperty<int>("JobId").HasColumnName("job_id");
                    });
        });

        modelBuilder.Entity<Resume>(entity =>
        {
            entity.HasKey(e => e.Username).HasName("PRIMARY");

            entity.ToTable("Resume");

            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .HasColumnName("username");
            entity.Property(e => e.Education)
                .HasMaxLength(300)
                .HasColumnName("education");
            entity.Property(e => e.JobExp)
                .HasMaxLength(300)
                .HasColumnName("Job_Exp");
            entity.Property(e => e.Projects)
                .HasMaxLength(300)
                .HasColumnName("projects");
            entity.Property(e => e.Recommendations)
                .HasMaxLength(300)
                .HasColumnName("recommendations");

            entity.HasOne(d => d.UsernameNavigation).WithOne(p => p.Resume)
                .HasForeignKey<Resume>(d => d.Username)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Resume_ibfk_1");
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(e => e.SkillId).HasName("PRIMARY");

            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.Category)
                .HasMaxLength(100)
                .HasColumnName("category");
            entity.Property(e => e.SkillName)
                .HasMaxLength(50)
                .HasColumnName("skill_name");

            entity.HasMany(d => d.Jobs).WithMany(p => p.Skills)
                .UsingEntity<Dictionary<string, object>>(
                    "Require",
                    r => r.HasOne<Job>().WithMany()
                        .HasForeignKey("JobId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Requires_ibfk_2"),
                    l => l.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Requires_ibfk_1"),
                    j =>
                    {
                        j.HasKey("SkillId", "JobId").HasName("PRIMARY");
                        j.ToTable("Requires");
                        j.HasIndex(new[] { "JobId" }, "job_id");
                        j.IndexerProperty<int>("SkillId").HasColumnName("skill_id");
                        j.IndexerProperty<int>("JobId").HasColumnName("job_id");
                    });

            entity.HasMany(d => d.Usernames).WithMany(p => p.Skills)
                .UsingEntity<Dictionary<string, object>>(
                    "Obtained",
                    r => r.HasOne<Profile>().WithMany()
                        .HasForeignKey("Username")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Obtained_ibfk_2"),
                    l => l.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("Obtained_ibfk_1"),
                    j =>
                    {
                        j.HasKey("SkillId", "Username").HasName("PRIMARY");
                        j.ToTable("Obtained");
                        j.HasIndex(new[] { "Username" }, "username");
                        j.IndexerProperty<int>("SkillId").HasColumnName("skill_id");
                        j.IndexerProperty<string>("Username")
                            .HasMaxLength(50)
                            .HasColumnName("username");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
