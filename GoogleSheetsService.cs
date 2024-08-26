using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GoogleSheetsService
{
    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets }; // Change this if you're accessing Drive or Docs
        private readonly SheetsService _sheetsService;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        private class MyHttpClientFactory : HttpClientFactory
        {
                
        }
        public GoogleSheetsService(ILogger<GoogleSheetsService> logger)
        {
            // Create Google Sheets API service.
            _logger = logger;
            var credential = GoogleCredential.GetApplicationDefault().CreateScoped(_scopes);

            var httpClientFactor = new HttpClientFactory();
            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
            });

            _sheetsService.HttpClient.Timeout = TimeSpan.FromSeconds(120);
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            try
            {
                // Build the request to read the data from the sheet
                var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!{range}");

                // Execute the request to read the data from the sheet
                var response = await request.ExecuteAsync();

                // Return the data as a list of lists of objects
                return response?.Values;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading sheetId: {sheetId}, sheet name: {sheetName} ", spreadsheetId, sheetName);
                throw;
            }
        }

        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string requestRange, int chunkSize = 1000)
        {
            // eg: A1:F9
            var rangeParts = requestRange.Split(":");

            // get A1
            string rangeFrom = rangeParts.First();

            // get A out of A1
            var columnFrom = rangeFrom.Substring(0, 1);

            // get 1 out of A1
            var rowFrom = rangeFrom.Length == 2 ? rangeFrom.Substring(1) : string.Empty;

            // get F9
            string rangeTo = rangeParts.Last();

            // get F out of F9
            var columnTo = rangeTo.Substring(0, 1);

            // get 9 out of F9, if rangeTo contains only 'F' then it's empty
            var rowTo = rangeTo.Length == 2 ? rangeTo.Substring(1) : string.Empty;

            // if rowFrom is empty then it's the first row (1)
            var rowFromParseResult = int.TryParse(rowFrom, out var rowFromCount);
            if (!rowFromParseResult)
            {
                rowFromCount = 1;
            }

            int rowToCount = chunkSize;

            var result = new List<IList<object>>();
            while (true)
            {
                string range = $"{sheetName}!{columnFrom}{rowFromCount}:{columnTo}{rowToCount}";
                try
                {
                    var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                    var response = await request.ExecuteAsync();
                    if (response == null || response.Values == null || !response.Values.Any())
                    {
                        break;
                    }

                    result.AddRange(response.Values);

                    // 'EU-Old'!A2:J969
                    var responseRangeInfo = response.Range;

                    // get A2:J969
                    string responseRange = responseRangeInfo.Substring(responseRangeInfo.IndexOf("!") + 1);

                    // Get 969
                    var rowReturned = responseRange.Split(":").Last().Substring(1);

                    var rowReturnedParseResult = int.TryParse(rowReturned, out var rowReturnedCount);

                    if (!rowReturnedParseResult || rowReturnedCount < rowToCount)
                    {
                        break;
                    }

                    rowFromCount = rowToCount + 1; // move from to next row
                    rowToCount += chunkSize; // move to to next chunk
                }
                catch (GoogleApiException ex)
                {
                    switch (ex.HttpStatusCode)
                    {
                        case HttpStatusCode.TooManyRequests:
                            _logger.LogInformation("Too many requests, waiting for 1 minute");
                            await Task.Delay(60_000);
                            break;

                        case HttpStatusCode.BadRequest when ex.Message.Contains("exceeds grid limits"):
                            return result;

                        default:
                            _logger.LogError(ex, "Error reading sheetId: {sheetId}, sheet name: {sheetName}  in chunks from range {range}", spreadsheetId, sheetName, range);
                            throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading sheetId: {sheetId}, sheet name: {sheetName}  in chunks from range {range}", spreadsheetId, sheetName, range);
                    throw;
                }
            }

            return result;
        }

        public async Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
        {
            try
            {
                // Build the request to write the data to the sheet
                var requestBody = new ValueRange
                {
                    Values = values
                };
                var request = _sheetsService.Spreadsheets.Values.Update(requestBody, spreadsheetId, $"{sheetName}!{range}");
                request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

                // Execute the request to write the data to the sheet
                await request.ExecuteAsync();

            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error writing sheetId: {sheetId}, sheet name: {sheetName}  at {range}", spreadsheetId, sheetName, range);
                throw;
            }

        }

        public async Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow)
        {
            try
            {
                var deleteDimensionRequest = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        Dimension = "ROWS",
                        StartIndex = fromRow - 1,
                    }
                };
                // specify spread sheet name to update
                var updateRequest = new Request
                {
                    DeleteDimension = deleteDimensionRequest
                };

                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = new List<Request> { updateRequest }
                };

                var request = _sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadSheetId);

                await request.ExecuteAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sheetId: {sheetId}, sheet name: {sheetName}  from row {fromRow}", spreadSheetId, spreadSheetName, fromRow);
                throw;
            }
        }

        public async Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                // Build the range string to write to the second row
                var range = "A2";

                // Call the WriteSheet method to write the data to the sheet
                await WriteSheetAsync(spreadsheetId, sheetName, range, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error write from second row for sheetId: {sheetId}, sheet name: {sheetName} ", spreadsheetId, sheetName);

                throw;
            }
        }

        public async Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                var sheetValues = await ReadSheetAsync(spreadsheetId, sheetName, "A2:Z");

                if (sheetValues?.Count > values.Count)
                {
                    var diff = sheetValues.Count - values.Count;
                    for (int i = 0; i < diff; i++)
                    {
                        var emptyValue = values[0].Select(x => (object)string.Empty).ToList();

                        values.Add(emptyValue);
                    }

                }

                await WriteFromSecondRowAsync(spreadsheetId, sheetName, values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing from second row for sheetId: {sheetId}, sheet name: {sheetName} ", spreadsheetId, sheetName);
                throw;
            }
        }

        public async Task WriteSheetAtLastRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            try
            {
                var lastRowRange = $"{sheetName}!A:A";

                var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, lastRowRange);

                // Execute the request to get the values in the last column of the sheet
                var response = await request.ExecuteAsync();

                // Get the last row index
                var lastRowIndex = response.Values?.Count ?? 0;

                var writeUntilIndex = lastRowIndex + 1;

                // Build the range string to write to the last row
                var range = $"A{writeUntilIndex}";


                // Call the WriteSheet method to write the data to the sheet
                await WriteSheetAsync(spreadsheetId, sheetName, range, values);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing at last row for sheetId: {sheetId}, sheet name: {sheetName} ", spreadsheetId, sheetName);
                throw;
            }
        }
    }

}