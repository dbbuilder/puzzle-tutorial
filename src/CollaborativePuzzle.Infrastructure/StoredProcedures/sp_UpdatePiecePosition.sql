-- =============================================
-- Author: Collaborative Puzzle Platform
-- Create date: Initial
-- Description: Updates puzzle piece position atomically
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpdatePiecePosition
    @PieceId UNIQUEIDENTIFIER,
    @X INT,
    @Y INT,
    @Rotation INT = 0,
    @UserId UNIQUEIDENTIFIER,
    @CheckPlacement BIT = 1,
    @SnapThreshold INT = 20,
    @Success BIT OUTPUT,
    @IsPlaced BIT OUTPUT,
    @FinalX INT OUTPUT,
    @FinalY INT OUTPUT,
    @FinalRotation INT OUTPUT,
    @ErrorMessage NVARCHAR(1000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @PuzzleId UNIQUEIDENTIFIER
    DECLARE @CorrectX INT
    DECLARE @CorrectY INT
    DECLARE @CurrentlyLocked UNIQUEIDENTIFIER
    DECLARE @CompletedPieces INT
    DECLARE @TotalPieces INT
    DECLARE @NewCompletionPercentage DECIMAL(5,2)
    DECLARE @WasPlaced BIT
    
    -- Initialize output parameters
    SET @Success = 0
    SET @IsPlaced = 0
    SET @ErrorMessage = NULL
    
    BEGIN TRY
        BEGIN TRANSACTION
        
        PRINT 'Updating piece position for piece: ' + CAST(@PieceId AS NVARCHAR(36))
        PRINT 'New position: X=' + CAST(@X AS NVARCHAR(10)) + ', Y=' + CAST(@Y AS NVARCHAR(10)) + ', Rotation=' + CAST(@Rotation AS NVARCHAR(10))
        
        -- Validate inputs
        IF @PieceId IS NULL OR @UserId IS NULL
        BEGIN
            SET @ErrorMessage = 'Piece ID and User ID cannot be null'
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN
        END
        
        -- Get piece information
        SELECT 
            @PuzzleId = PuzzleId,
            @CorrectX = CorrectX,
            @CorrectY = CorrectY,
            @CurrentlyLocked = LockedByUserId,
            @WasPlaced = IsPlaced
        FROM PuzzlePieces 
        WHERE Id = @PieceId
        
        IF @PuzzleId IS NULL
        BEGIN
            SET @ErrorMessage = 'Puzzle piece not found'
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN
        END
        
        -- Check if piece is locked by another user
        IF @CurrentlyLocked IS NOT NULL AND @CurrentlyLocked != @UserId
        BEGIN
            SET @ErrorMessage = 'Piece is currently locked by another user'
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN
        END
        
        -- Determine if piece should be placed (snap to correct position)
        SET @IsPlaced = 0
        SET @FinalX = @X
        SET @FinalY = @Y
        SET @FinalRotation = @Rotation
        
        IF @CheckPlacement = 1 AND @Rotation = 0
        BEGIN
            -- Check if position is close enough to correct position
            IF ABS(@X - @CorrectX) <= @SnapThreshold AND ABS(@Y - @CorrectY) <= @SnapThreshold
            BEGIN
                SET @IsPlaced = 1
                SET @FinalX = @CorrectX
                SET @FinalY = @CorrectY
                SET @FinalRotation = 0
                
                PRINT 'Piece snapped to correct position - placed successfully'
            END
        END
        
        -- Update piece position
        UPDATE PuzzlePieces
        SET 
            CurrentX = @FinalX,
            CurrentY = @FinalY,
            Rotation = @FinalRotation,
            IsPlaced = @IsPlaced,
            UpdatedAt = GETUTCDATE(),
            -- Clear lock when piece is moved
            LockedByUserId = NULL,
            LockedAt = NULL
        WHERE Id = @PieceId
        
        IF @@ROWCOUNT = 0
        BEGIN
            SET @ErrorMessage = 'Failed to update piece position'
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN
        END
        
        -- If piece was newly placed, update session progress
        IF @IsPlaced = 1 AND @WasPlaced = 0
        BEGIN
            -- Calculate new completion percentage for all sessions with this puzzle
            SELECT @TotalPieces = PieceCount FROM Puzzles WHERE Id = @PuzzleId
            SELECT @CompletedPieces = COUNT(*) FROM PuzzlePieces WHERE PuzzleId = @PuzzleId AND IsPlaced = 1
            
            SET @NewCompletionPercentage = CASE 
                WHEN @TotalPieces > 0 THEN (CAST(@CompletedPieces AS DECIMAL(10,2)) / @TotalPieces) * 100
                ELSE 0 
            END
            
            PRINT 'Progress updated: ' + CAST(@CompletedPieces AS NVARCHAR(10)) + '/' + CAST(@TotalPieces AS NVARCHAR(10)) + ' pieces (' + CAST(@NewCompletionPercentage AS NVARCHAR(10)) + '%)'
            
            -- Update all active sessions for this puzzle
            UPDATE PuzzleSessions
            SET 
                CompletedPieces = @CompletedPieces,
                CompletionPercentage = @NewCompletionPercentage,
                TotalMoves = TotalMoves + 1,
                LastActivityAt = GETUTCDATE(),
                CompletedAt = CASE WHEN @NewCompletionPercentage >= 100 THEN GETUTCDATE() ELSE CompletedAt END,
                Status = CASE WHEN @NewCompletionPercentage >= 100 THEN 3 ELSE Status END -- 3 = Completed
            WHERE PuzzleId = @PuzzleId 
            AND Status = 1 -- 1 = Active
        END
        ELSE
        BEGIN
            -- Just update move count for active sessions
            UPDATE PuzzleSessions
            SET 
                TotalMoves = TotalMoves + 1,
                LastActivityAt = GETUTCDATE()
            WHERE PuzzleId = @PuzzleId 
            AND Status = 1 -- 1 = Active
        END
        
        -- Update participant statistics
        UPDATE SessionParticipants
        SET 
            PiecesMoved = PiecesMoved + 1,
            PiecesPlaced = CASE WHEN @IsPlaced = 1 AND @WasPlaced = 0 THEN PiecesPlaced + 1 ELSE PiecesPlaced END,
            LastActivityAt = GETUTCDATE()
        WHERE UserId = @UserId 
        AND SessionId IN (
            SELECT Id FROM PuzzleSessions 
            WHERE PuzzleId = @PuzzleId AND Status = 1
        )
        
        SET @Success = 1
        
        COMMIT TRANSACTION
        
        PRINT 'Piece position updated successfully'
        
        -- Return result summary
        SELECT 
            @Success AS Success,
            @IsPlaced AS IsPlaced,
            @WasPlaced AS WasAlreadyPlaced,
            @FinalX AS FinalX,
            @FinalY AS FinalY,
            @FinalRotation AS FinalRotation,
            ISNULL(@CompletedPieces, 0) AS CompletedPieces,
            ISNULL(@NewCompletionPercentage, 0) AS CompletionPercentage,
            CASE WHEN @NewCompletionPercentage >= 100 THEN 1 ELSE 0 END AS PuzzleCompleted
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
        
        SET @Success = 0
        SET @ErrorMessage = ERROR_MESSAGE()
        
        PRINT 'Error updating piece position: ' + @ErrorMessage
        
        -- Return error result
        SELECT 
            @Success AS Success,
            0 AS IsPlaced,
            0 AS WasAlreadyPlaced,
            @X AS FinalX,
            @Y AS FinalY,
            @Rotation AS FinalRotation,
            0 AS CompletedPieces,
            0 AS CompletionPercentage,
            0 AS PuzzleCompleted,
            @ErrorMessage AS ErrorMessage
        
        RAISERROR (@ErrorMessage, 16, 1)
    END CATCH
END
