namespace GoogleSheetsService
{
    public interface ITimeProvider
    {
        Task Delay(TimeSpan delay, CancellationToken cancellationToken = default);
    }
}
