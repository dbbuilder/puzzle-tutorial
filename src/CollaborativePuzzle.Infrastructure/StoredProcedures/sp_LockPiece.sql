-- =============================================
-- Author: Collaborative Puzzle Platform
-- Create date: Initial
-- Description: Lock a puzzle piece for exclusive editing
-- =============================================
CREATE OR ALTER PROCEDURE sp_LockPiece
    @PieceId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @LockDurationMinutes INT = 5,
    @Success BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @ErrorMessage NVARCHAR(4000)
    DECLARE @CurrentLock UNIQUEIDENTIFIER
    DECLARE @LockTime DATETIME2
    
    SET @Success = 0
    
    BEGIN TRY
        BEGIN TRANSACTION
        
        PRINT 'Attempting to lock piece: ' + CAST(@PieceId AS NVARCHAR(36)) + ' for user: ' + CAST(@UserId AS NVARCHAR(36))
        
        -- Validate inputs
        IF @PieceId IS NULL OR @UserId IS NULL
        BEGIN
            RAISERROR('Piece ID and User ID cannot be null', 16, 1)
            RETURN
        END
        
        -- Check if piece exists
        SELECT @CurrentLock = LockedByUserId, @LockTime = LockedAt
        FROM PuzzlePieces 
        WHERE Id = @PieceId
        
        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Puzzle piece not found', 16, 1)
            RETURN
        END
        
        -- Check if piece is already locked by another user
        IF @CurrentLock IS NOT NULL AND @CurrentLock != @UserId
        BEGIN
            -- Check if lock has expired
            IF DATEDIFF(MINUTE, @LockTime, GETUTCDATE()) < @LockDurationMinutes
            BEGIN
                PRINT 'Piece is currently locked by another user and lock has not expired'
                SET @Success = 0
                COMMIT TRANSACTION
                RETURN
            END
            ELSE
            BEGIN
                PRINT 'Previous lock has expired, allowing new lock'
            END
        END
        
        -- Lock the piece
        UPDATE PuzzlePieces
        SET 
            LockedByUserId = @UserId,
            LockedAt = GETUTCDATE(),
            UpdatedAt = GETUTCDATE()
        WHERE Id = @PieceId
        
        IF @@ROWCOUNT > 0
        BEGIN
            SET @Success = 1
            PRINT 'Successfully locked piece for user'
        END
        ELSE
        BEGIN
            PRINT 'Failed to lock piece - update failed'
            SET @Success = 0
        END
        
        COMMIT TRANSACTION
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
            
        SELECT @ErrorMessage = ERROR_MESSAGE()
        
        PRINT 'Error locking piece: ' + @ErrorMessage
        SET @Success = 0
        
        RAISERROR (@ErrorMessage, 16, 1)
    END CATCH
END
