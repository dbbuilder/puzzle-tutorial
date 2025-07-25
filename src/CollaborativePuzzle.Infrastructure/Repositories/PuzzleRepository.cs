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
    /// Repository implementation for puzzle data access using Dapper and stored procedures only
    /// </summary>
    public class PuzzleRepository : IPuzzleRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PuzzleRepository> _logger;

        public PuzzleRepository(string connectionString, ILogger<PuzzleRepository> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Puzzle> CreatePuzzleAsync(Puzzle puzzle, IEnumerable<PuzzlePiece> pieces)
        {
            try
            {
                _logger.LogInformation("Creating puzzle '{Title}' with {PieceCount} pieces", puzzle.Title, puzzle.PieceCount);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Create puzzle using stored procedure
                    var puzzleParameters = new DynamicParameters();
                    puzzleParameters.Add("@Title", puzzle.Title, DbType.String, size: 200);
                    puzzleParameters.Add("@Description", puzzle.Description, DbType.String, size: 1000);
                    puzzleParameters.Add("@CreatedByUserId", puzzle.CreatedByUserId, DbType.Guid);
                    puzzleParameters.Add("@ImageUrl", puzzle.ImageUrl, DbType.String, size: 500);
                    puzzleParameters.Add("@PiecesDataUrl", puzzle.PiecesDataUrl, DbType.String, size: 500);
                    puzzleParameters.Add("@ImageFileName", puzzle.ImageFileName, DbType.String, size: 200);
                    puzzleParameters.Add("@ImageContentType", puzzle.ImageContentType, DbType.String, size: 50);
                    puzzleParameters.Add("@ImageSizeBytes", puzzle.ImageSizeBytes, DbType.Int64);
                    puzzleParameters.Add("@PieceCount", puzzle.PieceCount, DbType.Int32);
                    puzzleParameters.Add("@Width", puzzle.Width, DbType.Int32);
                    puzzleParameters.Add("@Height", puzzle.Height, DbType.Int32);
                    puzzleParameters.Add("@GridColumns", puzzle.GridColumns, DbType.Int32);
                    puzzleParameters.Add("@GridRows", puzzle.GridRows, DbType.Int32);
                    puzzleParameters.Add("@Difficulty", (int)puzzle.Difficulty, DbType.Int32);
                    puzzleParameters.Add("@EstimatedCompletionMinutes", puzzle.EstimatedCompletionMinutes, DbType.Int32);
                    puzzleParameters.Add("@Category", puzzle.Category, DbType.String, size: 100);
                    puzzleParameters.Add("@Tags", puzzle.Tags, DbType.String, size: 500);
                    puzzleParameters.Add("@IsPublic", puzzle.IsPublic, DbType.Boolean);
                    puzzleParameters.Add("@PuzzleId", dbType: DbType.Guid, direction: ParameterDirection.Output);

                    var result = await connection.QueryFirstOrDefaultAsync(
                        "sp_CreatePuzzle",
                        puzzleParameters,
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 60);

                    var puzzleId = puzzleParameters.Get<Guid>("@PuzzleId");
                    puzzle.Id = puzzleId;

                    if (result?.Status == "Error")
                    {
                        throw new InvalidOperationException($"Failed to create puzzle: {result.Message}");
                    }

                    // Insert puzzle pieces in batches
                    await InsertPuzzlePiecesAsync(connection, transaction, puzzleId, pieces);

                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully created puzzle {PuzzleId} with {PieceCount} pieces", 
                        puzzleId, puzzle.PieceCount);

                    return puzzle;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error creating puzzle '{Title}'", puzzle.Title);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating puzzle '{Title}'", puzzle.Title);
                throw;
            }
        }

        public async Task<Puzzle?> GetPuzzleByIdAsync(Guid puzzleId)
        {
            try
            {
                _logger.LogDebug("Retrieving puzzle {PuzzleId}", puzzleId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzleId, DbType.Guid);

                var puzzle = await connection.QueryFirstOrDefaultAsync<Puzzle>(
                    "sp_GetPuzzleById",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                if (puzzle != null)
                {
                    _logger.LogDebug("Retrieved puzzle {PuzzleId}: '{Title}'", puzzleId, puzzle.Title);
                }
                else
                {
                    _logger.LogWarning("Puzzle {PuzzleId} not found", puzzleId);
                }

                return puzzle;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving puzzle {PuzzleId}", puzzleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving puzzle {PuzzleId}", puzzleId);
                throw;
            }
        }

        public async Task<Puzzle?> GetPuzzleWithPiecesAsync(Guid puzzleId)
        {
            try
            {
                _logger.LogDebug("Retrieving puzzle {PuzzleId} with pieces", puzzleId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzleId, DbType.Guid);

                // Execute stored procedure that returns multiple result sets
                using var multi = await connection.QueryMultipleAsync(
                    "sp_GetPuzzleWithPieces",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var puzzle = await multi.ReadFirstOrDefaultAsync<Puzzle>();
                if (puzzle == null)
                {
                    _logger.LogWarning("Puzzle {PuzzleId} not found", puzzleId);
                    return null;
                }

                var pieces = await multi.ReadAsync<PuzzlePiece>();
                puzzle.Pieces = pieces.ToList();

                _logger.LogDebug("Retrieved puzzle {PuzzleId} with {PieceCount} pieces", 
                    puzzleId, puzzle.Pieces.Count);

                return puzzle;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving puzzle {PuzzleId} with pieces", puzzleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving puzzle {PuzzleId} with pieces", puzzleId);
                throw;
            }
        }

        public async Task<IEnumerable<Puzzle>> GetPublicPuzzlesAsync(int skip, int take, string? category = null, string? difficulty = null)
        {
            try
            {
                _logger.LogDebug("Retrieving public puzzles: skip={Skip}, take={Take}, category={Category}, difficulty={Difficulty}", 
                    skip, take, category, difficulty);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@Skip", skip, DbType.Int32);
                parameters.Add("@Take", take, DbType.Int32);
                parameters.Add("@Category", category, DbType.String, size: 100);
                parameters.Add("@Difficulty", difficulty, DbType.String, size: 50);

                var puzzles = await connection.QueryAsync<Puzzle>(
                    "sp_GetPublicPuzzles",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                _logger.LogDebug("Retrieved {Count} public puzzles", puzzles.Count());
                return puzzles;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving public puzzles");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving public puzzles");
                throw;
            }
        }

        public async Task<IEnumerable<Puzzle>> GetPuzzlesByUserAsync(Guid userId, int skip, int take)
        {
            try
            {
                _logger.LogDebug("Retrieving puzzles for user {UserId}: skip={Skip}, take={Take}", userId, skip, take);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@UserId", userId, DbType.Guid);
                parameters.Add("@Skip", skip, DbType.Int32);
                parameters.Add("@Take", take, DbType.Int32);

                var puzzles = await connection.QueryAsync<Puzzle>(
                    "sp_GetPuzzlesByUser",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                _logger.LogDebug("Retrieved {Count} puzzles for user {UserId}", puzzles.Count(), userId);
                return puzzles;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error retrieving puzzles for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving puzzles for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UpdatePuzzleAsync(Puzzle puzzle)
        {
            try
            {
                _logger.LogDebug("Updating puzzle {PuzzleId}", puzzle.Id);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzle.Id, DbType.Guid);
                parameters.Add("@Title", puzzle.Title, DbType.String, size: 200);
                parameters.Add("@Description", puzzle.Description, DbType.String, size: 1000);
                parameters.Add("@Category", puzzle.Category, DbType.String, size: 100);
                parameters.Add("@Tags", puzzle.Tags, DbType.String, size: 500);
                parameters.Add("@IsPublic", puzzle.IsPublic, DbType.Boolean);
                parameters.Add("@IsFeatured", puzzle.IsFeatured, DbType.Boolean);
                parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_UpdatePuzzle",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                var success = parameters.Get<bool>("@Success");

                if (success)
                {
                    _logger.LogInformation("Successfully updated puzzle {PuzzleId}", puzzle.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to update puzzle {PuzzleId}", puzzle.Id);
                }

                return success;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error updating puzzle {PuzzleId}", puzzle.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating puzzle {PuzzleId}", puzzle.Id);
                throw;
            }
        }

        public async Task<bool> DeletePuzzleAsync(Guid puzzleId)
        {
            try
            {
                _logger.LogInformation("Deleting puzzle {PuzzleId}", puzzleId);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@PuzzleId", puzzleId, DbType.Guid);
                parameters.Add("@Success", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_DeletePuzzle",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 60);

                var success = parameters.Get<bool>("@Success");

                if (success)
                {
                    _logger.LogInformation("Successfully deleted puzzle {PuzzleId}", puzzleId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete puzzle {PuzzleId}", puzzleId);
                }

                return success;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error deleting puzzle {PuzzleId}", puzzleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting puzzle {PuzzleId}", puzzleId);
                throw;
            }
        }

        public async Task<IEnumerable<Puzzle>> SearchPuzzlesAsync(string searchTerm, int skip, int take)
        {
            try
            {
                _logger.LogDebug("Searching puzzles with term '{SearchTerm}': skip={Skip}, take={Take}", 
                    searchTerm, skip, take);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new DynamicParameters();
                parameters.Add("@SearchTerm", searchTerm, DbType.String, size: 200);
                parameters.Add("@Skip", skip, DbType.Int32);
                parameters.Add("@Take", take, DbType.Int32);

                var puzzles = await connection.QueryAsync<Puzzle>(
                    "sp_SearchPuzzles",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 30);

                _logger.LogDebug("Found {Count} puzzles matching search term '{SearchTerm}'", 
                    puzzles.Count(), searchTerm);

                return puzzles;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error searching puzzles with term '{SearchTerm}'", searchTerm);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching puzzles with term '{SearchTerm}'", searchTerm);
                throw;
            }
        }

        /// <summary>
        /// Helper method to insert puzzle pieces in batches for better performance
        /// </summary>
        private async Task InsertPuzzlePiecesAsync(SqlConnection connection, SqlTransaction transaction, Guid puzzleId, IEnumerable<PuzzlePiece> pieces)
        {
            _logger.LogDebug("Inserting puzzle pieces for puzzle {PuzzleId}", puzzleId);

            var piecesList = pieces.ToList();
            const int batchSize = 100;

            for (int i = 0; i < piecesList.Count; i += batchSize)
            {
                var batch = piecesList.Skip(i).Take(batchSize);
                
                foreach (var piece in batch)
                {
                    piece.PuzzleId = puzzleId;
                    piece.Id = Guid.NewGuid();
                    piece.CreatedAt = DateTime.UtcNow;

                    var parameters = new DynamicParameters();
                    parameters.Add("@PieceId", piece.Id, DbType.Guid);
                    parameters.Add("@PuzzleId", piece.PuzzleId, DbType.Guid);
                    parameters.Add("@PieceNumber", piece.PieceNumber, DbType.Int32);
                    parameters.Add("@GridX", piece.GridX, DbType.Int32);
                    parameters.Add("@GridY", piece.GridY, DbType.Int32);
                    parameters.Add("@CorrectX", piece.CorrectX, DbType.Int32);
                    parameters.Add("@CorrectY", piece.CorrectY, DbType.Int32);
                    parameters.Add("@CurrentX", piece.CurrentX, DbType.Int32);
                    parameters.Add("@CurrentY", piece.CurrentY, DbType.Int32);
                    parameters.Add("@Rotation", piece.Rotation, DbType.Int32);
                    parameters.Add("@ShapeData", piece.ShapeData, DbType.String);
                    parameters.Add("@ImageX", piece.ImageX, DbType.Int32);
                    parameters.Add("@ImageY", piece.ImageY, DbType.Int32);
                    parameters.Add("@ImageWidth", piece.ImageWidth, DbType.Int32);
                    parameters.Add("@ImageHeight", piece.ImageHeight, DbType.Int32);
                    parameters.Add("@IsEdgePiece", piece.IsEdgePiece, DbType.Boolean);
                    parameters.Add("@IsCornerPiece", piece.IsCornerPiece, DbType.Boolean);

                    await connection.ExecuteAsync(
                        "sp_CreatePuzzlePiece",
                        parameters,
                        transaction: transaction,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 30);
                }

                _logger.LogDebug("Inserted batch of {BatchSize} pieces ({Processed}/{Total})", 
                    batch.Count(), Math.Min(i + batchSize, piecesList.Count), piecesList.Count);
            }

            _logger.LogInformation("Successfully inserted {TotalPieces} pieces for puzzle {PuzzleId}", 
                piecesList.Count, puzzleId);
        }
    }
}
