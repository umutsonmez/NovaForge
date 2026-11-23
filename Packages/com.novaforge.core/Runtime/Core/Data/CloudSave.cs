using System;
using System.Threading;
using System.Threading.Tasks;

namespace NovaForge.Core.Data
{
    public sealed class CloudSnapshot
    {
        public string Key;
        public string Content;
        public DateTime SavedAtUtc;
        /// <summary>Monotonic counter maintained by the game (e.g. total play sessions) used for conflict resolution.</summary>
        public long ProgressScore;
    }

    public enum CloudSaveStatus
    {
        Ok,
        NotSignedIn,
        NetworkError,
        Conflict,
        NotAvailable,
    }

    /// <summary>
    /// Abstraction over a cloud save backend (Google Play Games Saved Games, iCloud / Game Center, custom backend).
    /// NO concrete provider ships in Core yet; local saves work fully offline without it.
    /// </summary>
    public interface ICloudSaveProvider
    {
        string Name { get; }
        bool IsAvailable { get; }
        Task<CloudSaveStatus> UploadAsync(CloudSnapshot snapshot, CancellationToken cancellationToken);
        Task<(CloudSaveStatus status, CloudSnapshot snapshot)> DownloadAsync(string key, CancellationToken cancellationToken);
    }

    public enum ConflictChoice { KeepLocal, UseCloud }

    public interface ICloudConflictResolver
    {
        ConflictChoice Resolve(CloudSnapshot local, CloudSnapshot cloud);
    }

    /// <summary>Default: the snapshot with more progress wins; ties go to the most recent save.</summary>
    public sealed class MostProgressConflictResolver : ICloudConflictResolver
    {
        public ConflictChoice Resolve(CloudSnapshot local, CloudSnapshot cloud)
        {
            if (cloud == null) return ConflictChoice.KeepLocal;
            if (local == null) return ConflictChoice.UseCloud;
            if (cloud.ProgressScore != local.ProgressScore)
                return cloud.ProgressScore > local.ProgressScore ? ConflictChoice.UseCloud : ConflictChoice.KeepLocal;
            return cloud.SavedAtUtc > local.SavedAtUtc ? ConflictChoice.UseCloud : ConflictChoice.KeepLocal;
        }
    }
}
