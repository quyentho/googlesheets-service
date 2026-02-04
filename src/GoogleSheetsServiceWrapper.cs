using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using static Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.AppendRequest;

namespace GoogleSheetsService
{
    /// <summary>
    /// Concrete wrapper around Google Sheets API SheetsService.
    /// </summary>
    public class GoogleSheetsServiceWrapper : ISheetsServiceWrapper
    {
        private readonly SheetsService _sheetsService;

        public GoogleSheetsServiceWrapper(SheetsService sheetsService)
        {
            _sheetsService = sheetsService;
        }

        public async Task<ValueRange?> GetValuesAsync(string spreadsheetId, string range)
        {
            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
            return await request.ExecuteAsync();
        }

        public async Task WriteValuesAsync(string spreadsheetId, string range, ValueRange valueRange)
        {
            var request = _sheetsService.Spreadsheets.Values.Append(valueRange, spreadsheetId, range);
            request.ValueInputOption = ValueInputOptionEnum.RAW;
            request.InsertDataOption = InsertDataOptionEnum.OVERWRITE;
            await request.ExecuteAsync();
        }

        public async Task ClearValuesAsync(string spreadsheetId, string range)
        {
            var request = _sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, range);
            await request.ExecuteAsync();
        }

        public async Task BatchUpdateAsync(string spreadsheetId, BatchUpdateSpreadsheetRequest request)
        {
            var batchRequest = _sheetsService.Spreadsheets.BatchUpdate(request, spreadsheetId);
            await batchRequest.ExecuteAsync();
        }
    }
}
