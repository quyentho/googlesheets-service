namespace GoogleSheetsService.Resilience
{
    using Polly;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Decorator for IGoogleSheetsService that applies Polly v8 resilience pipeline to all operations.
    /// </summary>
    public class GoogleSheetsServiceWithResilience : IGoogleSheetsService
    {
        private readonly IGoogleSheetsService _innerService;
        private readonly ResiliencePipeline _pipeline;
        private readonly ILogger _logger;

        public GoogleSheetsServiceWithResilience(
            IGoogleSheetsService innerService,
            ResiliencePipeline pipeline,
            ILogger logger)
        {
            _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddSheetAsync(string spreadSheetId, string sheetName)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.AddSheetAsync(spreadSheetId, sheetName),
                CancellationToken.None
            );
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(
            string spreadsheetId,
            string sheetName,
            string range)
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReadSheetAsync(spreadsheetId, sheetName, range),
                CancellationToken.None
            );
        }

        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            int chunkSize = 1000)
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReadSheetInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    chunkSize
                ),
                CancellationToken.None
            );
        }

        public async Task<Dictionary<string, IList<IList<object>>>?> BatchGetValuesAsync(
            string spreadsheetId,
            string[] ranges)
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _innerService.BatchGetValuesAsync(spreadsheetId, ranges),
                CancellationToken.None
            );
        }

        public async Task WriteSheetAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.WriteSheetAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values
                ),
                CancellationToken.None
            );
        }

        public async Task AppendToEndAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values,
            string columnsRange = "A:Z")
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.AppendToEndAsync(
                    spreadsheetId,
                    sheetName,
                    values,
                    columnsRange
                ),
                CancellationToken.None
            );
        }

        public async Task WriteFromSecondRowAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.WriteFromSecondRowAsync(
                    spreadsheetId,
                    sheetName,
                    values
                ),
                CancellationToken.None
            );
        }

        public async Task DeleteRowsAsync(
            string spreadSheetId,
            string spreadSheetName,
            int fromRow)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.DeleteRowsAsync(
                    spreadSheetId,
                    spreadSheetName,
                    fromRow
                ),
                CancellationToken.None
            );
        }

        public async Task ReplaceFromSecondRowAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReplaceFromSecondRowAsync(
                    spreadsheetId,
                    sheetName,
                    values
                ),
                CancellationToken.None
            );
        }

        public async Task ReplaceFromSecondRowInChunksAsync(
            string spreadsheetId,
            string sheetName,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReplaceFromSecondRowInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    values,
                    chunkSize
                ),
                CancellationToken.None
            );
        }

        public async Task ReplaceFromRangeAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReplaceFromRangeAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values
                ),
                CancellationToken.None
            );
        }

        public async Task ClearValuesByRangeAsync(
            string spreadsheetId,
            string sheetName,
            string range)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.ClearValuesByRangeAsync(
                    spreadsheetId,
                    sheetName,
                    range
                ),
                CancellationToken.None
            );
        }

        public async Task WriteSheetInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.WriteSheetInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values,
                    chunkSize
                ),
                CancellationToken.None
            );
        }

        public async Task ReplaceFromRangeInChunksAsync(
            string spreadsheetId,
            string sheetName,
            string range,
            IList<IList<object>> values,
            int chunkSize)
        {
            await _pipeline.ExecuteAsync(
                async ct => await _innerService.ReplaceFromRangeInChunksAsync(
                    spreadsheetId,
                    sheetName,
                    range,
                    values,
                    chunkSize
                ),
                CancellationToken.None
            );
        }
    }
}
