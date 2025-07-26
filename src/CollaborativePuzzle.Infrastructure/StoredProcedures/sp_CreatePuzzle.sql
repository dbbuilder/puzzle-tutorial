-- =============================================
-- Author: Collaborative Puzzle Platform
-- Create date: Initial
-- Description: Creates a new puzzle with pieces
-- =============================================
CREATE OR ALTER PROCEDURE sp_CreatePuzzle
    @Title NVARCHAR(200),
    @Description NVARCHAR(1000) = NULL,
    @CreatedByUserId UNIQUEIDENTIFIER,
    @ImageUrl NVARCHAR(500),
    @PiecesDataUrl NVARCHAR(500),
    @ImageFileName NVARCHAR(200) = NULL,
    @ImageContentType NVARCHAR(50) = NULL,
    @ImageSizeBytes BIGINT = 0,
    @PieceCount INT,
    @Width INT,
    @Height INT,
    @GridColumns INT,
    @GridRows INT,
    @Difficulty INT,
    @EstimatedCompletionMinutes INT,
    @Category NVARCHAR(100) = NULL,
    @Tags NVARCHAR(500) = NULL,
    @IsPublic BIT = 1,
    @PuzzleId UNIQUEIDENTIFIER OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    
    DECLARE @ErrorMessage NVARCHAR(4000)
    DECLARE @ErrorSeverity INT
    DECLARE @ErrorState INT
    
    BEGIN TRY
        BEGIN TRANSACTION
        
        -- Generate new puzzle ID
        SET @PuzzleId = NEWID()
        
        PRINT 'Creating puzzle with ID: ' + CAST(@PuzzleId AS NVARCHAR(36))
        
        -- Validate inputs
        IF @Title IS NULL OR LEN(TRIM(@Title)) = 0
        BEGIN
            RAISERROR('Puzzle title cannot be empty', 16, 1)
            RETURN
        END
        
        IF @CreatedByUserId IS NULL
        BEGIN
            RAISERROR('Created by user ID cannot be null', 16, 1)
            RETURN
        END
        
        -- Verify user exists
        IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @CreatedByUserId AND IsActive = 1)
        BEGIN
            RAISERROR('User does not exist or is not active', 16, 1)
            RETURN
        END
        
        -- Insert puzzle record
        INSERT INTO Puzzles (
            Id, Title, Description, CreatedByUserId, ImageUrl, PiecesDataUrl,
            ImageFileName, ImageContentType, ImageSizeBytes, PieceCount,
            Width, Height, GridColumns, GridRows, Difficulty,
            EstimatedCompletionMinutes, Category, Tags, CreatedAt,
            IsPublic, IsActive, IsFeatured, TotalSessions, TotalCompletions,
            AverageCompletionTime, AverageRating, TotalRatings
        )
        VALUES (
            @PuzzleId, @Title, @Description, @CreatedByUserId, @ImageUrl, @PiecesDataUrl,
            @ImageFileName, @ImageContentType, @ImageSizeBytes, @PieceCount,
            @Width, @Height, @GridColumns, @GridRows, @Difficulty,
            @EstimatedCompletionMinutes, @Category, @Tags, GETUTCDATE(),
            @IsPublic, 1, 0, 0, 0, 0, 0.0, 0
        )
        
        -- Update user statistics
        UPDATE Users 
        SET TotalPuzzlesCreated = TotalPuzzlesCreated + 1,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @CreatedByUserId
        
        PRINT 'Puzzle created successfully. Total pieces to be generated: ' + CAST(@PieceCount AS NVARCHAR(10))
        
        COMMIT TRANSACTION
        
        -- Return success result
        SELECT 
            @PuzzleId AS PuzzleId,
            'Success' AS Status,
            'Puzzle created successfully' AS Message
            
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION
            
        SELECT @ErrorMessage = ERROR_MESSAGE(),
               @ErrorSeverity = ERROR_SEVERITY(),
               @ErrorState = ERROR_STATE()
        
        PRINT 'Error creating puzzle: ' + @ErrorMessage
        
        -- Return error result
        SELECT 
            NULL AS PuzzleId,
            'Error' AS Status,
            @ErrorMessage AS Message
            
        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState)
    END CATCH
END
