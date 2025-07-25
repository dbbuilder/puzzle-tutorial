using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using CollaborativePuzzle.Core.Interfaces;
using CollaborativePuzzle.Core.Entities;
using CollaborativePuzzle.Core.Models;

namespace CollaborativePuzzle.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for puzzle piece data access using Dapper and stored procedures only
    /// </summary>
    public class PieceRepository : IPieceRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PieceRepository> _logger;

        public PieceRepository(string connectionString, ILogger<PieceRepository> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<PuzzlePiece>> GetPiecesByPuzzleIdAsync(Guid puzzleId)
        {
            try
            {
                _logger.LogDebug("Retrieving pieces for puzzle {PuzzleId}", puzzleId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzleId, DbType.Guid);

                var pieces = await connection.QueryAsync<PuzzlePiece>(
                    "sp_GetPiecesByPuzzleId",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                _logger.LogDebug("Retrieved {Count} pieces for puzzle {PuzzleId}", pieces.Count(), puzzleId);
                return pieces;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving pieces for puzzle {PuzzleId}", puzzleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pieces for puzzle {PuzzleId}", puzzleId);
                throw;
            }
        }

        public async Task<PuzzlePiece?> GetPieceByIdAsync(Guid pieceId)
        {
            try
            {
                _logger.LogDebug("Retrieving piece {PieceId}", pieceId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PieceId", pieceId, DbType.Guid);

                var piece = await connection.QueryFirstOrDefaultAsync<PuzzlePiece>(
                    "sp_GetPieceById",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                if (piece != null)
                {
                    _logger.LogDebug("Retrieved piece {PieceId}", pieceId);
                }
                else
                {
                    _logger.LogWarning("Piece {PieceId} not found", pieceId);
                }

                return piece;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving piece {PieceId}", pieceId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving piece {PieceId}", pieceId);
                throw;
            }
        }

        public async Task<PieceMoveResult> UpdatePiecePositionAsync(Guid pieceId, int x, int y, int rotation, bool isPlaced)
        {
            try
            {
                _logger.LogDebug("Updating piece {PieceId} position to ({X}, {Y}) with rotation {Rotation}", 
                    pieceId, x, y, rotation);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PieceId", pieceId, DbType.Guid);
                parameters.Add("@X", x, DbType.Int32);
                parameters.Add("@Y", y, DbType.Int32);
                parameters.Add("@Rotation", rotation, DbType.Int32);
                parameters.Add("@UserId", GetCurrentUserId(), DbType.Guid);
                parameters.Add("@CheckPlacement", true, DbType.Boolean);
                parameters.Add("@SnapThreshold", 20, DbType.Int32);
                parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                parameters.Add("@IsPlaced", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                parameters.Add("@FinalX", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@FinalY", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@FinalRotation", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@ErrorMessage", dbType: DbType.String, direction: ParameterDirection.Output, size: 1000);

                var result = await connection.QueryFirstOrDefaultAsync(
                    "sp_UpdatePiecePosition",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var success = parameters.Get<bool>("@Success");
                var finalIsPlaced = parameters.Get<bool>("@IsPlaced");
                var finalX = parameters.Get<int>("@FinalX");
                var finalY = parameters.Get<int>("@FinalY");
                var finalRotation = parameters.Get<int>("@FinalRotation");
                var errorMessage = parameters.Get<string>("@ErrorMessage");

                // Get updated progress information if successful
                int completedPieces = 0;
                decimal completionPercentage = 0;
                bool puzzleCompleted = false;

                if (success)
                {
                    // The stored procedure should return this information in the result set
                    if (result != null)
                    {
                        completedPieces = result.CompletedPieces ?? 0;
                        completionPercentage = result.CompletionPercentage ?? 0;
                        puzzleCompleted = result.PuzzleCompleted ?? false;
                    }

                    _logger.LogInformation("Piece {PieceId} updated successfully. Placed: {IsPlaced}, Progress: {Progress}%", 
                        pieceId, finalIsPlaced, completionPercentage);
                }
                else
                {
                    _logger.LogWarning("Failed to update piece {PieceId}: {ErrorMessage}", pieceId, errorMessage);
                }

                return new PieceMoveResult
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    IsPlaced = finalIsPlaced,
                    WasAlreadyPlaced = result?.WasAlreadyPlaced ?? false,
                    FinalX = finalX,
                    FinalY = finalY,
                    FinalRotation = finalRotation,
                    CompletedPieces = completedPieces,
                    CompletionPercentage = completionPercentage,
                    PuzzleCompleted = puzzleCompleted
                };
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error updating piece {PieceId} position", pieceId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating piece {PieceId} position", pieceId);
                throw;
            }
        }

        public async Task<bool> LockPieceAsync(Guid pieceId, Guid userId)
        {
            try
            {
                _logger.LogDebug("Attempting to lock piece {PieceId} for user {UserId}", pieceId, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PieceId", pieceId, DbType.Guid);
                parameters.Add("@UserId", userId, DbType.Guid);
                parameters.Add("@LockDurationMinutes", 5, DbType.Int32);
                parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_LockPiece",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var success = parameters.Get<bool>("@Success");

                if (success)
                {
                    _logger.LogDebug("Successfully locked piece {PieceId} for user {UserId}", pieceId, userId);
                }
                else
                {
                    _logger.LogDebug("Failed to lock piece {PieceId} for user {UserId} - may already be locked", pieceId, userId);
                }

                return success;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error locking piece {PieceId} for user {UserId}", pieceId, userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking piece {PieceId} for user {UserId}", pieceId, userId);
                throw;
            }
        }

        public async Task<bool> UnlockPieceAsync(Guid pieceId, Guid userId)
        {
            try
            {
                _logger.LogDebug("Attempting to unlock piece {PieceId} for user {UserId}", pieceId, userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PieceId", pieceId, DbType.Guid);
                parameters.Add("@UserId", userId, DbType.Guid);
                parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_UnlockPiece",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var success = parameters.Get<bool>("@Success");

                if (success)
                {
                    _logger.LogDebug("Successfully unlocked piece {PieceId} for user {UserId}", pieceId, userId);
                }
                else
                {
                    _logger.LogDebug("Failed to unlock piece {PieceId} for user {UserId} - may not own lock", pieceId, userId);
                }

                return success;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error unlocking piece {PieceId} for user {UserId}", pieceId, userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking piece {PieceId} for user {UserId}", pieceId, userId);
                throw;
            }
        }

        public async Task<IEnumerable<PuzzlePiece>> GetLockedPiecesByUserAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving locked pieces for user {UserId}", userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@UserId", userId, DbType.Guid);

                var pieces = await connection.QueryAsync<PuzzlePiece>(
                    "sp_GetLockedPiecesByUser",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                _logger.LogDebug("Retrieved {Count} locked pieces for user {UserId}", pieces.Count(), userId);
                return pieces;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving locked pieces for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving locked pieces for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> UnlockAllPiecesByUserAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Unlocking all pieces for user {UserId}", userId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@UserId", userId, DbType.Guid);
                parameters.Add("@UnlockedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_UnlockAllPiecesByUser",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var unlockedCount = parameters.Get<int>("@UnlockedCount");

                _logger.LogInformation("Unlocked {Count} pieces for user {UserId}", unlockedCount, userId);
                return unlockedCount;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error unlocking all pieces for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking all pieces for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> UnlockExpiredPiecesAsync(int timeoutMinutes)
        {
            try
            {
                _logger.LogDebug("Unlocking pieces with expired locks (timeout: {TimeoutMinutes} minutes)", timeoutMinutes);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@TimeoutMinutes", timeoutMinutes, DbType.Int32);
                parameters.Add("@UnlockedCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_UnlockExpiredPieces",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var unlockedCount = parameters.Get<int>("@UnlockedCount");

                if (unlockedCount > 0)
                {
                    _logger.LogInformation("Unlocked {Count} expired piece locks", unlockedCount);
                }

                return unlockedCount;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error unlocking expired pieces");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking expired pieces");
                throw;
            }
        }

        public async Task<(int CompletedPieces, decimal CompletionPercentage)> GetPuzzleProgressAsync(Guid puzzleId)
        {
            try
            {
                _logger.LogDebug("Getting progress for puzzle {PuzzleId}", puzzleId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzleId, DbType.Guid);
                parameters.Add("@CompletedPieces", dbType: DbType.Int32, direction: ParameterDirection.Output);
                parameters.Add("@CompletionPercentage", dbType: DbType.Decimal, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_GetPuzzleProgress",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var completedPieces = parameters.Get<int>("@CompletedPieces");
                var completionPercentage = parameters.Get<decimal>("@CompletionPercentage");

                _logger.LogDebug("Puzzle {PuzzleId} progress: {CompletedPieces} pieces, {CompletionPercentage}%", 
                    puzzleId, completedPieces, completionPercentage);

                return (completedPieces, completionPercentage);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error getting puzzle progress for {PuzzleId}", puzzleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting puzzle progress for {PuzzleId}", puzzleId);
                throw;
            }
        }

        /// <summary>
        /// Helper method to get current user ID from context
        /// In a real implementation, this would come from the current HTTP context or user claims
        /// </summary>
        private Guid GetCurrentUserId()
        {
            // This is a placeholder - in a real implementation, you would get this from:
            // - HttpContext.User claims
            // - Current authentication context
            // - Dependency injection of current user service
            return Guid.NewGuid(); // Temporary implementation
        }
    }
}
