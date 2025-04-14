namespace GoogleSheetsService
{
    public interface IGoogleSheetsService
    {
        Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range);
        Task<IList<IList<object>>?> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string range, int chunkSize = 1000);
        Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values);
        Task WriteFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
        Task DeleteRowsAsync(string spreadSheetId, string spreadSheetName, int fromRow);
        Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
        Task ReplaceFromSecondRowInChunksAsync(string spreadsheetId, string sheetName, IList<IList<object>> values, int chunkSize);
        Task ReplaceFromRangeAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values);
        Task AddSheetAsync(string spreadSheetId, string sheetName);
        Task ClearValuesByRangeAsync(string spreadsheetId, string sheetName, string range);
        Task WriteSheetInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize);
        Task ReplaceSheetInChunksAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values, int chunkSize);
    }
}