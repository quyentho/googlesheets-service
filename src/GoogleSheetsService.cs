using Google;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace GoogleSheetsService
{
    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly ISheetsServiceWrapper _sheetsServiceWrapper;
        private readonly ILogger _logger;
        private readonly ITimeProvider _timeProvider;

        public GoogleSheetsService(ILogger logger, SheetsService sheetsService)
            : this(logger, new GoogleSheetsServiceWrapper(sheetsService), new SystemTimeProvider())
        {
        }

        /// <summary>
        /// Constructor for dependency injection with custom ISheetsServiceWrapper implementation (useful for testing).
        /// </summary>
        public GoogleSheetsService(ILogger logger, ISheetsServiceWrapper sheetsServiceWrapper)
            : this(logger, sheetsServiceWrapper, new SystemTimeProvider())
        {
        }

        /// <summary>
        /// Constructor for dependency injection with custom ISheetsServiceWrapper and ITimeProvider implementations (useful for testing).
        /// </summary>
        public GoogleSheetsService(ILogger logger, ISheetsServiceWrapper sheetsServiceWrapper, ITimeProvider timeProvider)
        {
            _sheetsServiceWrapper = sheetsServiceWrapper;
            _logger = logger;
            _timeProvider = timeProvider;
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
                    await _timeProvider.Delay(TimeSpan.FromMinutes(1));
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
                    await _timeProvider.Delay(TimeSpan.FromMinutes(1));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
                {
                    // Return partial results on 400 Bad Request
                    return allRows.Count > 0 ? allRows : null;
                }
            }

            return allRows.Count > 0 ? allRows : null;
        }

        /// <summary>
        /// Streams data from a sheet in chunks to avoid loading all rows into memory.
        /// Supports open-ended ranges like "A2:AI" (no end row) or fixed ranges like "A1:Z100".
        /// </summary>
        /// <param name="spreadsheetId">The spreadsheet ID</param>
        /// <param name="sheetName">The sheet name</param>
        /// <param name="requestRange">Range in format "A2:AI" or "A1:Z100"</param>
        /// <param name="chunkSize">Number of rows to read per API call (default 1000)</param>
        /// <returns>Async stream of row chunks</returns>
        public async IAsyncEnumerable<IList<IList<object>>> ReadSheetChunksAsync(string spreadsheetId, string sheetName, string requestRange, int chunkSize = 1000)
        {
            _logger.LogDebug("[SHEETS-SERVICE] ReadSheetChunksAsync called: {SpreadsheetId}/{SheetName}, range: {Range}, chunkSize: {ChunkSize}",
                spreadsheetId, sheetName, requestRange, chunkSize);

            // Parse range to get start row and columns
            // Format: "A2:AI" means start at row 2, columns A to AI
            var parts = requestRange.Split(':');
            if (parts.Length != 2)
            {
                _logger.LogWarning("[SHEETS-SERVICE] Range format invalid (not 2 parts), falling back to ReadSheetAsync: {Range}", requestRange);
                var rows = await ReadSheetAsync(spreadsheetId, sheetName, requestRange);
                if (rows != null && rows.Count > 0)
                {
                    yield return rows;
                }

                yield break;
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

            _logger.LogDebug("[SHEETS-SERVICE] Parsed range - StartRow: {StartRow}, StartColumn: {StartColumn}, EndColumn: {EndColumn}",
                startRow, startColumn, endColumn);

            int currentRow = startRow;
            bool hasMoreData = true;
            int chunkCount = 0;

            async Task<IList<IList<object>>?> TryReadChunkAsync(string chunkRange)
            {
                while (true)
                {
                    try
                    {
                        var response = await _sheetsServiceWrapper.GetValuesAsync(spreadsheetId, chunkRange);
                        return response?.Values;
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogInformation("Too many requests, waiting for 1 minute");
                        await _timeProvider.Delay(TimeSpan.FromMinutes(1));
                    }
                    catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
                    {
                        return null;
                    }
                }
            }

            while (hasMoreData)
            {
                // Build chunk range: e.g., "A2:AI1001" for first chunk
                int endRow = currentRow + chunkSize - 1;
                string chunkRange = $"{sheetName}!{startColumn}{currentRow}:{endColumn}{endRow}";

                _logger.LogDebug("[SHEETS-SERVICE] Reading chunk {ChunkIndex}, range: {ChunkRange}", chunkCount, chunkRange);

                var values = await TryReadChunkAsync(chunkRange);
                if (values == null || values.Count == 0)
                {
                    _logger.LogDebug("[SHEETS-SERVICE] No more data (empty values), stopping iteration at chunk {ChunkIndex}", chunkCount);
                    hasMoreData = false;
                    break;
                }

                _logger.LogDebug("[SHEETS-SERVICE] Chunk {ChunkIndex} read successfully, {RowCount} rows", chunkCount, values.Count);
                yield return values;

                // If we got fewer rows than chunk size, we've reached the end
                if (values.Count < chunkSize)
                {
                    _logger.LogDebug("[SHEETS-SERVICE] Partial chunk received ({RowCount} < {ChunkSize}), stopping iteration", values.Count, chunkSize);
                    hasMoreData = false;
                }
                else
                {
                    currentRow = endRow + 1;
                }

                chunkCount++;
            }

            _logger.LogDebug("[SHEETS-SERVICE] ReadSheetChunksAsync completed, total chunks yielded: {ChunkCount}", chunkCount);
        }

        public async Task<Dictionary<string, IList<IList<object>>>?> BatchGetValuesAsync(string spreadsheetId, string[] ranges)
        {
            if (ranges == null || ranges.Length == 0)
            {
                return null;
            }

            while (true)
            {
                try
                {
                    var response = await _sheetsServiceWrapper.BatchGetValuesAsync(spreadsheetId, ranges);

                    if (response?.ValueRanges == null || response.ValueRanges.Count == 0)
                    {
                        return null;
                    }

                    var results = new Dictionary<string, IList<IList<object>>>(StringComparer.OrdinalIgnoreCase);

                    // Map response ranges to the requested range keys
                    // This handles Google's range expansion: "Sheet!A1:Z" → "Sheet!A1:Z999"
                    foreach (var valueRange in response.ValueRanges)
                    {
                        if (valueRange == null) continue;

                        var responseRange = valueRange.Range ?? string.Empty;
                        var matchingKey = RangeMatchingHelper.FindMatchingRangeKey(ranges, responseRange);
                        Debug.Assert(matchingKey != null, "Some mismatch between requested and response ranges that we don't know how to resolve");
                        matchingKey ??= responseRange;
                        results[matchingKey] = valueRange.Values ?? new List<IList<object>>();
                    }

                    return results;
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogInformation("Too many requests, waiting for 1 minute");
                    await _timeProvider.Delay(TimeSpan.FromMinutes(1));
                }
                catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.BadRequest)
                {
                    // Return partial results on 400 Bad Request
                    return null;
                }
            }
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

        /// <summary>
        /// Appends values to the sheet using Google Sheets append behavior to decide placement.
        /// Uses INSERT_ROWS to add new rows after the last non-empty row in the range.
        /// </summary>
        public async Task AppendToEndAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, string columnsRange = "A:Z")
        {
            if (values.Count == 0)
            {
                return;
            }

            var requestBody = new ValueRange
            {
                Values = values
            };

            await _sheetsServiceWrapper.AppendValuesAsync(spreadsheetId, $"{sheetName}!{columnsRange}", requestBody);
        }

        public async Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow)
        {
            var spreadsheet = await _sheetsServiceWrapper.GetSpreadsheetAsync(spreadSheetId);
            var sheet = spreadsheet?.Sheets?
                .FirstOrDefault(s => string.Equals(s.Properties?.Title, spreadSheetName, StringComparison.OrdinalIgnoreCase));

            if (sheet?.Properties?.SheetId == null)
            {
                throw new InvalidOperationException($"Sheet '{spreadSheetName}' was not found in spreadsheet '{spreadSheetId}'.");
            }

            var gridProperties = sheet.Properties.GridProperties;
            var rowCount = gridProperties?.RowCount ?? fromRow;
            var frozenRowCount = gridProperties?.FrozenRowCount ?? 0;
            var startIndex = fromRow - 1;
            var deleteStartIndex = Math.Max(startIndex, frozenRowCount);
            var endIndex = rowCount - 1;

            if (rowCount <= deleteStartIndex + 1)
            {
                return;
            }

            var deleteDimensionRequest = new DeleteDimensionRequest
            {
                Range = new DimensionRange
                {
                    SheetId = sheet.Properties.SheetId.Value,
                    Dimension = "ROWS",
                    StartIndex = deleteStartIndex,
                    EndIndex = endIndex
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
                    await _timeProvider.Delay(TimeSpan.FromMinutes(1));
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