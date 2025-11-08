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
    }
}
