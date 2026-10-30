namespace CulinaryBlog.Application.Common.Interfaces;

// Fire-and-forget removal of an object-storage file (SRS FR-RCP-008 step 14, FR-FILE-002).
// The implementation (Hangfire) calls IFileStorageService.DeleteAsync and retries on failure.
public interface IImageCleanupQueue
{
    void EnqueueDelete(string objectKey);
}
