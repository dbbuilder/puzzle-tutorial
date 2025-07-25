-- =============================================
-- Author: Collaborative Puzzle Platform
-- Create date: Initial
-- Description: Unlock a puzzle piece
-- =============================================
CREATE OR ALTER PROCEDURE sp_UnlockPiece
    @PieceId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Success BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @ErrorMessage NVARCHAR(4000)
    DECLARE @CurrentLock UNIQUEIDENTIFIER
    
    SET @Success = 0
    
    BEGIN TRY
        PRINT 'Attempting to unlock piece: ' + CAST(@PieceId AS NVARCHAR(36)) + ' for user: ' + CAST(@UserId AS NVARCHAR(36))
        
        -- Validate inputs
        IF @PieceId IS NULL OR @UserId IS NULL
        BEGIN
            RAISERROR('Piece ID and User ID cannot be null', 16, 1)
            RETURN
        END
        
        -- Check current lock owner
        SELECT @CurrentLock = LockedByUserId
        FROM PuzzlePieces 
        WHERE Id = @PieceId
        
        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Puzzle piece not found', 16, 1)
            RETURN
        END
        
        -- Check if user owns the lock
        IF @CurrentLock IS NULL
        BEGIN
            PRINT 'Piece is not currently locked'
            SET @Success = 1 -- Consider it successful if not locked
            RETURN
        END
        
        IF @CurrentLock != @UserId
        BEGIN
            PRINT 'User does not own the lock on this piece'
            SET @Success = 0
            RETURN
        END
        
        -- Unlock the piece
        UPDATE PuzzlePieces
        SET 
            LockedByUserId = NULL,
            LockedAt = NULL,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @PieceId AND LockedByUserId = @UserId
        
        IF @@ROWCOUNT > 0
        BEGIN
            SET @Success = 1
            PRINT 'Successfully unlocked piece'
        END
        ELSE
        BEGIN
            PRINT 'Failed to unlock piece - update failed'
            SET @Success = 0
        END
        
    END TRY
    BEGIN CATCH
        SELECT @ErrorMessage = ERROR_MESSAGE()
        
        PRINT 'Error unlocking piece: ' + @ErrorMessage
        SET @Success = 0
        
        RAISERROR (@ErrorMessage, 16, 1)
    END CATCH
END
