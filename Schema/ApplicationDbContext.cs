using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;



public class ApplicationDbContext: DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {

    }

    public DbSet<UserAccount> users { get; set; }
    public DbSet<Event> events { get; set; }
    public DbSet<EventOrganizer> eventOrganizers { get; set; }
    public DbSet<EventParticipant> eventParticipants { get; set; }
    public DbSet<EventRecording> eventRecordings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ensure the creation_timestamp column has a DB-side default of now() (Postgres)
        modelBuilder.Entity<UserAccount>(eb =>
        {
            eb.Property(e => e.creationTimestamp)
                .HasColumnName("creation_timestamp")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Event>(eb =>
        {
            eb.Property(e => e.CreationTimestamp)
                .HasColumnName("creation_timestamp")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();
        });

        // Configure EventOrganizer with cascade delete and unique constraint
        modelBuilder.Entity<EventOrganizer>(eb =>
        {
            eb.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();

            // FK to Event with CASCADE DELETE
            eb.HasOne(eo => eo.Event)
                .WithMany()
                .HasForeignKey(eo => eo.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // FK to UserAccount with CASCADE DELETE
            eb.HasOne(eo => eo.User)
                .WithMany()
                .HasForeignKey(eo => eo.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one user can be organizer of same event only once
            eb.HasIndex(eo => new { eo.EventId, eo.UserId })
                .IsUnique();
        });

        // Configure EventParticipant with cascade delete, timestamps, and partial unique constraints
        modelBuilder.Entity<EventParticipant>(eb =>
        {
            eb.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();

            eb.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAddOrUpdate();

            // FK to Event with CASCADE DELETE
            eb.HasOne(ep => ep.Event)
                .WithMany()
                .HasForeignKey(ep => ep.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // FK to UserAccount with CASCADE DELETE (nullable)
            eb.HasOne(ep => ep.User)
                .WithMany()
                .HasForeignKey(ep => ep.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Index on (event_id, status) for filtering
            eb.HasIndex(ep => new { ep.EventId, ep.Status });

            // Index on (event_id, created_at) for FIFO waitlist
            eb.HasIndex(ep => new { ep.EventId, ep.CreatedAt });

            // Index on normalized_email for lookups
            eb.HasIndex(ep => ep.NormalizedEmail);

            // Unique constraint for logged-in users: (event_id, user_id) where status is active
            // Note: Partial unique indexes with WHERE clauses need raw SQL in migration
            // We'll handle this in the migration SQL generation
        });

        // Configure Event with unique slug index
        modelBuilder.Entity<Event>(eb =>
        {
            eb.HasIndex(e => e.Slug)
                .IsUnique();
        });

        modelBuilder.Entity<EventRecording>(eb =>
        {
            eb.Property(r => r.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();

            eb.Property(r => r.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAddOrUpdate();

            eb.HasOne<Event>()
                .WithMany()
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            eb.HasIndex(r => new { r.EventId, r.CreatedAt });
        });
    }
}
