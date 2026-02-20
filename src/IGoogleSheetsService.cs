namespace GoogleSheetsService
{
    public interface IGoogleSheetsService
    {
        Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range);
        Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string range, int chunkSize = 1000);
        IAsyncEnumerable<IList<IList<object>>> ReadSheetChunksAsync(string spreadsheetId, string sheetName, string range, int chunkSize = 1000);
        /// <summary>
        /// Reads multiple ranges from the same spreadsheet in a single batch request.
        /// </summary>
        /// <param name="spreadsheetId">The spreadsheet ID.</param>
        /// <param name="ranges">Array of ranges in A1 notation (e.g., ["Sheet1!A1:Z", "Sheet2!A1:C"]).</param>
        /// <returns>Dictionary mapping each requested range to its returned values. Or null if no data was retrieved.</returns>
        /// <remarks>
        /// Be careful and only use this method for those operations that you don't need to get back the exact original requested ranges
        /// Because Google Sheets API may modify the requested ranges in the response (e.g., expanding "Sheet!A1:Z" to "Sheet!A1:Z999")
        /// And this method only tries it best to match the requested ranges to the response ranges and will return whatever Google returns if it cannot find an exact match.
        /// </remarks>
        Task<Dictionary<string, IList<IList<object>>>?> BatchGetValuesAsync(string spreadsheetId, string[] ranges);
        Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values);

        /// <summary>
        /// Appends values to the sheet using Google Sheets append behavior to decide placement.
        /// Uses INSERT_ROWS to add new rows after the last non-empty row in the range.
        /// </summary>
        /// <param name="spreadsheetId">The spreadsheet ID.</param>
        /// <param name="sheetName">The sheet name.</param>
        /// <param name="values">The values to append.</param>
        /// <param name="columnsRange">Column span in A1 notation (e.g., "A:Z").</param>
        Task AppendToEndAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, string columnsRange = "A:Z");
        Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
        Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow);
        Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
        Task ReplaceFromSecondRowInChunksAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, int chunkSize);
        Task ReplaceFromRangeAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values);
        Task AddSheetAsync(string spreadSheetId, string sheetName);
        Task ClearValuesByRangeAsync(string spreadsheetId, string sheetName, string range);
        Task WriteSheetInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize);
        Task ReplaceFromRangeInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize);
    }
}