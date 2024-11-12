﻿using Google;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GoogleSheetsService
{
    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly SheetsService _sheetsService;
        private readonly ILogger _logger;

        public GoogleSheetsService(ILogger logger, SheetsService sheetsService)
        {
            _sheetsService = sheetsService;
            _logger = logger;
        }

        public async Task AddSheetAsync(string spreadSheetId, string sheetName)
        {
            BatchUpdateSpreadsheetRequest body = new BatchUpdateSpreadsheetRequest();

            body.Requests = new List<Request>
            {
                new Request
                {
                    AddSheet = new AddSheetRequest()
                    {
                        Properties = new SheetProperties
                        {
                            Title = sheetName
                        }
                    }
                }
            };

            var batchUpdateRequest = _sheetsService.Spreadsheets.BatchUpdate(body, spreadSheetId);

            await batchUpdateRequest.ExecuteAsync();
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            while (true)
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
                catch (GoogleApiException ex) when ( ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                {
                        _logger.LogInformation("Too many requests, waiting for 1 minute");
                        await Task.Delay(60_000);
                }
            }
        }

        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string requestRange, int chunkSize = 1000)
        {
            // eg: A1:F9
            // rangeFrom: A1, rangeTo: F9
            (string rangeFrom, string rangeTo) = SplitRange(requestRange);

            // get A out of A1
            var columnFrom = ExtractColumnFromRange(rangeFrom);
            // get F out of F9
            var columnTo = ExtractColumnFromRange(rangeTo);

            // get 1 out of A1
            string rowFrom = ExtractRowFromRange(rangeFrom);

            // if rowFrom is empty then it's the first row (1)
            var rowFromParseResult = int.TryParse(rowFrom, out var rowFromCount);
            if (!rowFromParseResult)
            {
                rowFromCount = 1;
            }

            // fetch to first chunk
            int rowToCount = chunkSize;

            var result = new List<IList<object>>();
            while (true)
            {
                // range: 'sheetName'!A1:F1000
                string range = GetRange(sheetName, columnFrom, columnTo, rowFromCount, rowToCount);
                try
                {
                    var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
                    var response = await request.ExecuteAsync();
                    if (response == null || response.Values == null || !response.Values.Any())
                    {
                        break;
                    }

                    result.AddRange(response.Values);

                    // 'sheetName'!A2:J969
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
                    }
                }
            }

            return result;
        }

        private static string GetRange(string sheetName, string columnFrom, string columnTo, int rowFromCount, int rowToCount)
        {
            return $"{sheetName}!{columnFrom}{rowFromCount}:{columnTo}{rowToCount}";
        }

        private static string ExtractRowFromRange(string rangeFrom)
        {
            // get 9 out of F9, if rangeTo contains only 'F' then it's empty
            return rangeFrom.Length == 2 ? rangeFrom.Substring(1) : string.Empty;
        }

        private static string ExtractColumnFromRange(string range)
        {
            return range.Substring(0, 1);
        }

        private static (string rangeFrom, string rangeTo) SplitRange(string requestRange)
        {
            string[] rangeParts = requestRange.Split(":");

            string rangeFrom = rangeParts.First();
            string rangeTo = rangeParts.Last();

            return (rangeFrom, rangeTo);
        }

        public async Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
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

        public async Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow)
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

        public async Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            // Build the range string to write to the second row
            var range = "A2";

            // Call the WriteSheet method to write the data to the sheet
            await WriteSheetAsync(spreadsheetId, sheetName, range, values);
        }

        public async Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
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

        public async Task WriteSheetAtLastRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
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
    }

}