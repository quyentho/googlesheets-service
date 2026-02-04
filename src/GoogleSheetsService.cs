using Google;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GoogleSheetsService
{
    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly ISheetsServiceWrapper _sheetsServiceWrapper;
        private readonly ILogger _logger;

        public GoogleSheetsService(ILogger logger, SheetsService sheetsService)
            : this(logger, new GoogleSheetsServiceWrapper(sheetsService))
        {
        }

        /// <summary>
        /// Constructor for dependency injection with custom ISheetsServiceWrapper implementation (useful for testing).
        /// </summary>
        public GoogleSheetsService(ILogger logger, ISheetsServiceWrapper sheetsServiceWrapper)
        {
            _sheetsServiceWrapper = sheetsServiceWrapper;
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

            await _sheetsServiceWrapper.BatchUpdateAsync(spreadSheetId, body);
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            while (true)
            {
                try
                {
                    // Build the request to read the data from the sheet
                    var response = await _sheetsServiceWrapper.GetValuesAsync(spreadsheetId, $"{sheetName}!{range}");

                    // Return the data as a list of lists of objects
                    return response?.Values;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogInformation("Too many requests, waiting for 1 minute");
                    await Task.Delay(60_000);
                }
            }
        }

        /// <summary>
        /// Reads data from a sheet in chunks to reduce memory allocation for large datasets.
        /// Supports open-ended ranges like "A2:AI" (no end row) or fixed ranges like "A1:Z100".
        /// </summary>
        /// <param name="spreadsheetId">The spreadsheet ID</param>
        /// <param name="sheetName">The sheet name</param>
        /// <param name="requestRange">Range in format "A2:AI" or "A1:Z100"</param>
        /// <param name="chunkSize">Number of rows to read per API call (default 1000)</param>
        /// <returns>Accumulated rows from all chunks, or null if no data was retrieved</returns>
        public async Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string requestRange, int chunkSize = 1000)
        {
            // Parse range to get start row and columns
            // Format: "A2:AI" means start at row 2, columns A to AI
            var parts = requestRange.Split(':');
            if (parts.Length != 2)
            {
                // For invalid ranges, fall back to single read
                return await ReadSheetAsync(spreadsheetId, sheetName, requestRange);
            }

            var startCell = parts[0];
            var endColumn = parts[1];

            // Extract start row number from startCell (e.g., "A2" -> 2)
            var startRowStr = new string(startCell.Where(char.IsDigit).ToArray());
            if (!int.TryParse(startRowStr, out int startRow))
            {
                startRow = 1;
            }

            // Extract start column (e.g., "A2" -> "A", "AA2" -> "AA")
            var startColumn = new string(startCell.Where(char.IsLetter).ToArray());

            var allRows = new List<IList<object>>();
            int currentRow = startRow;
            bool hasMoreData = true;

            while (hasMoreData)
            {
                // Build chunk range: e.g., "A2:AI1001" for first chunk
                int endRow = currentRow + chunkSize - 1;
                string chunkRange = $"{sheetName}!{startColumn}{currentRow}:{endColumn}{endRow}";

                try
                {
                    var response = await _sheetsServiceWrapper.GetValuesAsync(spreadsheetId, chunkRange);

                    if (response?.Values == null || response.Values.Count == 0)
                    {
                        hasMoreData = false;
                        break;
                    }

                    // Add all rows from this chunk
                    allRows.AddRange(response.Values);

                    // If we got fewer rows than chunk size, we've reached the end
                    if (response.Values.Count < chunkSize)
                    {
                        hasMoreData = false;
                    }
                    else
                    {
                        currentRow = endRow + 1;
                    }
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogInformation("Too many requests, waiting for 1 minute");
                    await Task.Delay(60_000);
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
                {
                    // Return partial results on 400 Bad Request
                    return allRows.Count > 0 ? allRows : null;
                }
            }

            return allRows.Count > 0 ? allRows : null;
        }



        public async Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
        {
            // Build the request to write the data to the sheet
            var requestBody = new ValueRange
            {
                Values = values
            };

            await _sheetsServiceWrapper.WriteValuesAsync(spreadsheetId, $"{sheetName}!{range}", requestBody);
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

            await _sheetsServiceWrapper.BatchUpdateAsync(spreadSheetId, batchUpdateRequest);
        }

        public async Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            // Build the range string to write to the second row
            var range = "A2";

            // Call the WriteSheet method to write the data to the sheet
            await WriteSheetAsync(spreadsheetId, sheetName, range, values);
        }

        public async Task ReplaceFromSecondRowInChunksAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, int chunkSize)
        {
            await ClearValuesByRangeAsync(spreadsheetId, sheetName, "A2:Z");

            await WriteSheetInChunksAsync(spreadsheetId, sheetName, "A2", values, chunkSize);
        }
        public async Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            await ClearValuesByRangeAsync(spreadsheetId, sheetName, "A2:Z");

            await WriteFromSecondRowAsync(spreadsheetId, sheetName, values);
        }
        public async Task ClearValuesByRangeAsync(string spreadsheetId, string sheetName, string range)
        {
            await _sheetsServiceWrapper.ClearValuesAsync(spreadsheetId, $"{sheetName}!{range}");
        }
        public async Task ReplaceFromRangeInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize)
        {
            await ClearValuesByRangeAsync(spreadsheetId, sheetName, range);

            await WriteSheetInChunksAsync(spreadsheetId, sheetName, range, values, chunkSize);
        }

        public async Task WriteSheetInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize)
        {
            var rangeFrom = range.Split(":").First();
            var columnFrom = rangeFrom[..1];
            var rowFrom = rangeFrom.Substring(1);

            var rowCount = int.Parse(rowFrom);

            while (true)
            {
                try
                {
                    // initialize the range to be the first row
                    foreach (var chunk in values.Chunk(chunkSize))
                    {
                        await WriteSheetAsync(spreadsheetId, sheetName, rangeFrom, chunk);

                        rowCount += chunk.Length;
                        rangeFrom = $"{columnFrom}{rowCount + 1}";
                    }

                    return;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogInformation("Too many write requests, waiting for 1 minute");
                    await Task.Delay(60_000);
                }
            }
        }

        public async Task ReplaceFromRangeAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values)
        {
            await ClearValuesByRangeAsync(spreadsheetId, sheetName, range);

            await WriteSheetAsync(spreadsheetId, sheetName, range, values);
        }
    }

}