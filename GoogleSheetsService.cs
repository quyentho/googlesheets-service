namespace GoogleSheetsService
{
    using Google.Apis.Auth.OAuth2;
    using Google.Apis.Services;
    using Google.Apis.Sheets.v4;
    using Google.Apis.Sheets.v4.Data;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;

    public class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly string[] _scopes = { SheetsService.Scope.Spreadsheets }; // Change this if you're accessing Drive or Docs
        private readonly SheetsService _sheetsService;
        public GoogleSheetsService()
        {
            // Create Google Sheets API service.
            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = GoogleCredential.GetApplicationDefault().CreateScoped(_scopes)
            });
        }

        public async Task<IList<IList<object>>?> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            // Build the request to read the data from the sheet
            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!{range}");

            // Execute the request to read the data from the sheet
            var response = await request.ExecuteAsync();

            // Return the data as a list of lists of objects
            return response?.Values;
        }

        public async Task<IList<IList<object>>> ReadSheetInChunksAsync(string spreadsheetId, string sheetName, string range)
        {
            // A1:F9
            var rangeParts = range.Split(":");

            // get F out of F9
            var column = rangeParts.Last().Substring(0, 1);

            const int chunkSize = 100;
            int count = chunkSize;
            
            var result = new List<IList<object>>();
            while (true)
            {
                var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!{rangeParts.First()}:{column}{count}");
                var response =  await request.ExecuteAsync();
                if (response == null || response.Values == null || response.Values.Count == 0)
                {
                    break;
                }
                
                result.AddRange(response.Values);
                count += chunkSize;
            }

            return result;
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