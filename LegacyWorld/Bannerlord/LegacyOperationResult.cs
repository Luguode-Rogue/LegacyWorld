namespace LegacyWorld.Bannerlord
{
    public enum LegacyOperationStatus
    {
        Applied,
        Partial,
        Skipped,
        Failed
    }

    /// <summary>一次导入/应用操作的真实结果，供行为层决定 UI 与持久化状态。</summary>
    public sealed class LegacyOperationResult
    {
        public LegacyOperationStatus Status { get; }
        public string Message { get; }

        public bool FullyApplied => Status == LegacyOperationStatus.Applied;
        public bool Executed => Status == LegacyOperationStatus.Applied || Status == LegacyOperationStatus.Partial;

        private LegacyOperationResult(LegacyOperationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public static LegacyOperationResult Applied(string message)
            => new LegacyOperationResult(LegacyOperationStatus.Applied, message);

        public static LegacyOperationResult Partial(string message)
            => new LegacyOperationResult(LegacyOperationStatus.Partial, message);

        public static LegacyOperationResult Skipped(string message)
            => new LegacyOperationResult(LegacyOperationStatus.Skipped, message);

        public static LegacyOperationResult Failed(string message)
            => new LegacyOperationResult(LegacyOperationStatus.Failed, message);
    }
}
