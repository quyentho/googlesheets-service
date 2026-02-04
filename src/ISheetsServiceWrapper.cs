using Google.Apis.Sheets.v4.Data;

namespace GoogleSheetsService
{
    /// <summary>
    /// Wrapper interface for Google Sheets API operations to enable testing without actual API calls.
    /// </summary>
    public interface ISheetsServiceWrapper
    {
        /// <summary>
        /// Gets data from a spreadsheet range.
        /// </summary>
        Task<ValueRange?> GetValuesAsync(string spreadsheetId, string range);

        /// <summary>
        /// Gets data from multiple ranges in a spreadsheet in a single batch request.
        /// </summary>
        Task<BatchGetValuesResponse?> BatchGetValuesAsync(string spreadsheetId, IList<string> ranges);

        /// <summary>
        /// Writes data to a spreadsheet range, overwriting existing data.
        /// Uses RAW input and OVERWRITE mode.
        /// </summary>
        Task WriteValuesAsync(string spreadsheetId, string range, ValueRange valueRange);

        /// <summary>
        /// Clears data from a spreadsheet range.
        /// </summary>
        Task ClearValuesAsync(string spreadsheetId, string range);

        /// <summary>
        /// Batch updates a spreadsheet.
        /// </summary>
        Task BatchUpdateAsync(string spreadsheetId, BatchUpdateSpreadsheetRequest request);
    }
}
