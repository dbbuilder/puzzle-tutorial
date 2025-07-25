-- =============================================
-- Author: Collaborative Puzzle Platform
-- Create date: Initial
-- Description: Retrieves puzzle with all pieces
-- =============================================
CREATE OR ALTER PROCEDURE sp_GetPuzzleWithPieces
    @PuzzleId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @ErrorMessage NVARCHAR(4000)
    
    BEGIN TRY
        PRINT 'Retrieving puzzle with pieces for ID: ' + CAST(@PuzzleId AS NVARCHAR(36))
        
        -- Validate input
        IF @PuzzleId IS NULL
        BEGIN
            RAISERROR('Puzzle ID cannot be null', 16, 1)
            RETURN
        END
        
        -- Check if puzzle exists
        IF NOT EXISTS (SELECT 1 FROM Puzzles WHERE Id = @PuzzleId AND IsActive = 1)
        BEGIN
            RAISERROR('Puzzle not found or is not active', 16, 1)
            RETURN
        END
        
        -- Return puzzle information
        SELECT 
            p.Id,
            p.Title,
            p.Description,
            p.CreatedByUserId,
            u.Username AS CreatedByUsername,
            u.DisplayName AS CreatedByDisplayName,
            p.ImageUrl,
            p.PiecesDataUrl,
            p.ImageFileName,
            p.ImageContentType,
            p.ImageSizeBytes,
            p.PieceCount,
            p.Width,
            p.Height,
            p.GridColumns,
            p.GridRows,
            p.Difficulty,
            p.EstimatedCompletionMinutes,
            p.Category,
            p.Tags,
            p.CreatedAt,
            p.UpdatedAt,
            p.IsPublic,
            p.IsFeatured,
            p.TotalSessions,
            p.TotalCompletions,
            p.AverageCompletionTime,
            p.AverageRating,
            p.TotalRatings
        FROM Puzzles p
        INNER JOIN Users u ON p.CreatedByUserId = u.Id
        WHERE p.Id = @PuzzleId AND p.IsActive = 1
        
        -- Return all puzzle pieces
        SELECT 
            pp.Id,
            pp.PuzzleId,
            pp.PieceNumber,
            pp.GridX,
            pp.GridY,
            pp.CorrectX,
            pp.CorrectY,
            pp.CurrentX,
            pp.CurrentY,
            pp.Rotation,
            pp.ShapeData,
            pp.ImageX,
            pp.ImageY,
            pp.ImageWidth,
            pp.ImageHeight,
            pp.IsPlaced,
            pp.IsEdgePiece,
            pp.IsCornerPiece,
            pp.LockedByUserId,
            pp.LockedAt,
            pp.CreatedAt,
            pp.UpdatedAt,
            lu.Username AS LockedByUsername
        FROM PuzzlePieces pp
        LEFT JOIN Users lu ON pp.LockedByUserId = lu.Id
        WHERE pp.PuzzleId = @PuzzleId
        ORDER BY pp.PieceNumber
        
        PRINT 'Successfully retrieved puzzle and ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' pieces'
        
    END TRY
    BEGIN CATCH
        SELECT @ErrorMessage = ERROR_MESSAGE()
        
        PRINT 'Error retrieving puzzle with pieces: ' + @ErrorMessage
        
        RAISERROR (@ErrorMessage, 16, 1)
    END CATCH
END
