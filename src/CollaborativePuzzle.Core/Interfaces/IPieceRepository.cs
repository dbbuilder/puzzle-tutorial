        /// <summary>
        /// Gets the current progress statistics for a puzzle session
        /// </summary>
        /// <param name="puzzleId">Puzzle identifier</param>
        /// <returns>Progress statistics including completion percentage</returns>
        Task<(int CompletedPieces, decimal CompletionPercentage)> GetPuzzleProgressAsync(Guid puzzleId);
    }
}
