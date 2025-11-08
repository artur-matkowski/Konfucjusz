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
    }
}