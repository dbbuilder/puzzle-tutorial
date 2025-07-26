using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollaborativePuzzle.Core.Interfaces
{
    /// <summary>
    /// Service interface for Azure Blob Storage operations
    /// Used for storing puzzle images and generated piece data
    /// </summary>
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads an image file to blob storage
        /// </summary>
        /// <param name="containerName">Storage container name</param>
        /// <param name="fileName">File name for the blob</param>
        /// <param name="content">File content as byte array</param>
        /// <param name="contentType">MIME type of the file</param>
        /// <returns>URL of the uploaded blob</returns>
        Task<string> UploadImageAsync(string containerName, string fileName, byte[] content, string contentType);
        
        /// <summary>
        /// Downloads an image file from blob storage
        /// </summary>
        /// <param name="containerName">Storage container name</param>
        /// <param name="fileName">File name of the blob</param>
        /// <returns>File content as byte array</returns>
        Task<byte[]> DownloadImageAsync(string containerName, string fileName);
        
        /// <summary>
        /// Deletes an image file from blob storage
        /// </summary>
        /// <param name="containerName">Storage container name</param>
        /// <param name="fileName">File name of the blob</param>
        /// <returns>True if deletion was successful</returns>
        Task<bool> DeleteImageAsync(string containerName, string fileName);
        
        /// <summary>
        /// Uploads puzzle piece data as JSON to blob storage
        /// </summary>
        /// <param name="containerName">Storage container name</param>
        /// <param name="fileName">File name for the blob</param>
        /// <param name="pieceData">Piece data as JSON string</param>
        /// <returns>URL of the uploaded blob</returns>
        Task<string> UploadPieceDataAsync(string containerName, string fileName, string pieceData);
        
        /// <summary>
        /// Gets a secure URL for direct client access to a blob
        /// </summary>
        /// <param name="containerName">Storage container name</param>
        /// <param name="fileName">File name of the blob</param>
        /// <param name="expiryHours">Number of hours the URL should remain valid</param>
        /// <returns>Secure URL with SAS token</returns>
        Task<string> GetSecureUrlAsync(string containerName, string fileName, int expiryHours = 24);
    }
}
