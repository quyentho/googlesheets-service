namespace GoogleSheetsService
{
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(delay, cancellationToken);
    }
}
