namespace GoogleSheetsService
{
    public interface IGoogleSheetsService
    {
        Task<IList<IList<object>>> ReadSheetAsync(string spreadsheetId, string sheetName, string range);
        Task WriteSheetAsync(string spreadsheetId, string sheetName, string range, IList<IList<object>> values);
        Task WriteSheetAtLastRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
        Task ReplaceFromSecondRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values);
    }
}