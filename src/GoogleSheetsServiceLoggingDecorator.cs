using Google.Apis.Http;
using Microsoft.Extensions.Logging;

namespace GoogleSheetsService
{
    public class GoogleSheetsServiceLoggingDecorator : IGoogleSheetsService
    {
        private const string Message = "Error perform Google Sheets operation with sheetId: {spreadSheetId}, sheetName: {sheetName}";
        private const string MessageWithRange = "Error perform Google Sheets operation with sheetId: {spreadSheetId}, sheetName: {sheetName}, range {range}";
        private readonly ILogger _logger;
        private readonly IGoogleSheetsService _decoratee;

        private class MyHttpClientFactory : HttpClientFactory
        {

        }
        public GoogleSheetsServiceLoggingDecorator(ILogger logger, IGoogleSheetsService decoratee)
        {
            _logger = logger;
            _decoratee = decoratee;
        }

        public async Task AddSheetAsync(string spreadSheetId, string sheetName)
        {
            try
            {
                await _decoratee.AddSheetAsync(spreadSheetId, sheetName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadSheetId, sheetName);
                throw;
            }
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            try
            {
                // Build the request to read the data from the sheet
                return await _decoratee.ReadSheetAsync(spreadsheetId, sheetName, range);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, range);
                throw;
            }
        }

        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string requestRange, int chunkSize = 1000)
        {
            try
            {
                return await _decoratee.ReadSheetInChunksAsync(spreadsheetId, sheetName, requestRange, chunkSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, requestRange);
                throw;
            }
        }

        public async Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
        {
            try
            {
                await _decoratee.WriteSheetAsync(spreadsheetId, sheetName, range, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, range);
                throw;
            }
        }

        public async Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow)
        {
            try
            {
                await _decoratee.DeleteRowsAsync(spreadSheetId, spreadSheetName, fromRow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message + " from row: {row}", spreadSheetId, spreadSheetName, fromRow);
                throw;
            }
        }

        public async Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                await _decoratee.WriteFromSecondRowAsync(spreadsheetId, sheetName, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadsheetId, sheetName);
                throw;
            }
        }

        public async Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                await _decoratee.ReplaceFromSecondRowAsync(spreadsheetId, sheetName, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadsheetId, sheetName);
                throw;
            }
        }
        public async Task ClearValuesByRangeAsync(string spreadsheetId, string sheetName, string range)
        {

            try
            {
                    await _decoratee.ClearValuesByRangeAsync(spreadsheetId, sheetName, range);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, range);
                throw;
            }
        }

        public async Task ReplaceFromSecondRowInChunksAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, int chunkSize)
        {
            try
            {
                await _decoratee.ReplaceFromSecondRowInChunksAsync(spreadsheetId, sheetName, values, chunkSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadsheetId, sheetName);
                throw;
            }
        }

        public async Task ReplaceFromRangeAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
        {
            try
            {
                await _decoratee.ReplaceFromRangeAsync(spreadsheetId, sheetName, range, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadsheetId, sheetName);
                throw;
            }
        }

        public async Task AppendFromRangeAsync(string spreadsheetId, string sheetName, string fromRange, IList<IList<object>> values)
        {

            try
            {
                await _decoratee.AppendFromRangeAsync(spreadsheetId, sheetName, fromRange, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, fromRange);
                throw;
            }
        }

        public async Task ReplaceFromRangeInChunkAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize)
        {
            try
            {
                await _decoratee.ReplaceFromRangeInChunkAsync(spreadsheetId, sheetName, range, values, chunkSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, MessageWithRange, spreadsheetId, sheetName, range);
                throw;
            }
        }
    }

}