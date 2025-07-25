using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CollaborativePuzzle.Core.Entities;

namespace CollaborativePuzzle.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework database context for the Collaborative Puzzle Platform
    /// Configured to use stored procedures only, no LINQ queries
    /// </summary>
    public class PuzzleDbContext : DbContext
    {
        private readonly ILogger<PuzzleDbContext> _logger;

        public PuzzleDbContext(DbContextOptions<PuzzleDbContext> options, ILogger<PuzzleDbContext> logger)
            : base(options)
        {
            _logger = logger;
        }

        // DbSets for all entities
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Puzzle> Puzzles { get; set; } = null!;
        public DbSet<PuzzlePiece> PuzzlePieces { get; set; } = null!;
        public DbSet<PuzzleSession> PuzzleSessions { get; set; } = null!;
        public DbSet<SessionParticipant> SessionParticipants { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.ExternalId);
                entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
                entity.Property(e => e.DisplayName).HasMaxLength(255);
                entity.Property(e => e.AvatarUrl).HasMaxLength(500);
                entity.Property(e => e.ExternalId).HasMaxLength(50);
                entity.Property(e => e.Provider).HasMaxLength(100);
                entity.Property(e => e.PreferredLanguage).HasMaxLength(10).HasDefaultValue("en");
                
                // Configure relationships
                entity.HasMany(e => e.CreatedPuzzles)
                    .WithOne(e => e.CreatedByUser)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.CreatedSessions)
                    .WithOne(e => e.CreatedByUser)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Puzzle entity
            modelBuilder.Entity<Puzzle>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CreatedByUserId);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Difficulty);
                entity.HasIndex(e => e.IsPublic);
                entity.HasIndex(e => e.CreatedAt);
                
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.ImageUrl).HasMaxLength(500).IsRequired();
                entity.Property(e => e.PiecesDataUrl).HasMaxLength(500).IsRequired();
                entity.Property(e => e.ImageFileName).HasMaxLength(200);
                entity.Property(e => e.ImageContentType).HasMaxLength(50);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Tags).HasMaxLength(500);
                entity.Property(e => e.AverageCompletionTime).HasConversion(
                    v => v.TotalMilliseconds,
                    v => TimeSpan.FromMilliseconds(v));

                // Configure relationships
                entity.HasMany(e => e.Pieces)
                    .WithOne(e => e.Puzzle)
                    .HasForeignKey(e => e.PuzzleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Sessions)
                    .WithOne(e => e.Puzzle)
                    .HasForeignKey(e => e.PuzzleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure PuzzlePiece entity
            modelBuilder.Entity<PuzzlePiece>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PuzzleId);
                entity.HasIndex(e => e.PieceNumber);
                entity.HasIndex(e => e.LockedByUserId);
                entity.HasIndex(e => e.IsPlaced);
                
                entity.Property(e => e.ShapeData).IsRequired();
                entity.Property(e => e.RowVersion).IsRowVersion();

                // Configure relationships
                entity.HasOne(e => e.LockedByUser)
                    .WithMany(e => e.LockedPieces)
                    .HasForeignKey(e => e.LockedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure PuzzleSession entity
            modelBuilder.Entity<PuzzleSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.PuzzleId);
                entity.HasIndex(e => e.CreatedByUserId);
                entity.HasIndex(e => e.JoinCode).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsPublic);
                entity.HasIndex(e => e.CreatedAt);
                
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.JoinCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Password).HasMaxLength(500);
                entity.Property(e => e.CompletionPercentage).HasPrecision(5, 2);
                entity.Property(e => e.RowVersion).IsRowVersion();

                // Configure relationships
                entity.HasMany(e => e.Participants)
                    .WithOne(e => e.Session)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.ChatMessages)
                    .WithOne(e => e.Session)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure SessionParticipant entity
            modelBuilder.Entity<SessionParticipant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ConnectionId);
                
                // Ensure unique user per session
                entity.HasIndex(e => new { e.SessionId, e.UserId }).IsUnique();
                
                entity.Property(e => e.ConnectionId).HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.TotalActiveTime).HasConversion(
                    v => v.TotalMilliseconds,
                    v => TimeSpan.FromMilliseconds(v));
                entity.Property(e => e.RowVersion).IsRowVersion();
            });

            // Configure ChatMessage entity
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SessionId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ReplyToMessageId);
                
                entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Language).HasMaxLength(50);
                
                // Configure self-referencing relationship for message replies
                entity.HasOne(e => e.ReplyToMessage)
                    .WithMany()
                    .HasForeignKey(e => e.ReplyToMessageId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.DeletedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.DeletedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            _logger.LogDebug("Entity Framework model configuration completed");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Enable sensitive data logging in development
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Add audit information before saving
                AddAuditInformation();
                
                var result = await base.SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Successfully saved {ChangeCount} changes to database", result);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict occurred while saving changes");
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update exception occurred while saving changes");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while saving changes");
                throw;
            }
        }

        private void AddAuditInformation()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is User || e.Entity is Puzzle || e.Entity is PuzzleSession)
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity is User user)
                    {
                        user.CreatedAt = DateTime.UtcNow;
                        user.LastActiveAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is Puzzle puzzle)
                    {
                        puzzle.CreatedAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is PuzzleSession session)
                    {
                        session.CreatedAt = DateTime.UtcNow;
                        session.LastActivityAt = DateTime.UtcNow;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is User user)
                    {
                        user.UpdatedAt = DateTime.UtcNow;
                        user.LastActiveAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is Puzzle puzzle)
                    {
                        puzzle.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is PuzzleSession session)
                    {
                        session.LastActivityAt = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}
