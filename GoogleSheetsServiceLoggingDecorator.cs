using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System.Net;

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
        public GoogleSheetsServiceLoggingDecorator(ILogger<GoogleSheetsServiceLoggingDecorator> logger, IGoogleSheetsService decoratee)
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

        public async Task WriteSheetAtLastRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                await _decoratee.WriteSheetAtLastRowAsync(spreadsheetId, sheetName, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, Message, spreadsheetId, sheetName);
                throw;
            }
        }
    }

}