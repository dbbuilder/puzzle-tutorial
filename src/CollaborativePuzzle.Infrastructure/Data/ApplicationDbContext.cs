using CollaborativePuzzle.Core.Entities;
using Microsoft.EntityFrameworkCore;
using User = CollaborativePuzzle.Core.Models.User;
using Role = CollaborativePuzzle.Core.Models.Role;
using UserRole = CollaborativePuzzle.Core.Models.UserRole;

namespace CollaborativePuzzle.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Authentication models
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    
    // Puzzle entities
    public DbSet<Puzzle> Puzzles { get; set; }
    public DbSet<PuzzleSession> PuzzleSessions { get; set; }
    public DbSet<PuzzlePiece> PuzzlePieces { get; set; }
    public DbSet<SessionParticipant> SessionParticipants { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure UserRole join table
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId);
                
            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId);
        });

        // Seed default roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = "1", Name = "Admin", Description = "Full system access" },
            new Role { Id = "2", Name = "User", Description = "Regular user access" },
            new Role { Id = "3", Name = "Player", Description = "Puzzle player access" }
        );
    }
}