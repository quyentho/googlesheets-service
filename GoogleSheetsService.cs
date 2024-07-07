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
        private readonly string _applicationName = "My Application Name from Google API Project ";
        private readonly SheetsService _sheetsService;
        public GoogleSheetsService()
        {
            GoogleCredential credential;

            // Put your credentials json file in the root of the solution and make sure copy to output dir property is set to always copy 
            using (var stream = new FileStream(
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "client_secrets.json"),
                FileMode.Open, FileAccess.Read
                ))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(_scopes);
            }

            // Create Google Sheets API service.
            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });

        }

        public async Task<IList<IList<object>>> ReadSheetAsync(string spreadsheetId, string sheetName, string range)
        {
            // Build the request to read the data from the sheet
            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!{range}");

            // Execute the request to read the data from the sheet
            var response = await request.ExecuteAsync();

            // Return the data as a list of lists of objects
            return response.Values;
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

        public async Task WriteSheetAtLastRowAsync(string spreadsheetId, string sheetName, IList<IList<object>> values)
        {
            var lastRowRange = $"{sheetName}!A:A";

            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, lastRowRange);

            // Execute the request to get the values in the last column of the sheet
            var response = await request.ExecuteAsync();

            // Get the last row index
            var lastRowIndex = response.Values.Count;

            var writeUntilIndex = lastRowIndex + 1;

            // Build the range string to write to the last row
            var range = $"A{writeUntilIndex}";


            // Call the WriteSheet method to write the data to the sheet
            await WriteSheetAsync(spreadsheetId, sheetName, range, values);
        }
    }

}