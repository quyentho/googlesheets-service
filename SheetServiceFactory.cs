using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace GoogleSheetsService
{
    public class SheetServiceFactory : ISheetServiceFactory
    {
        private static readonly string[] _scopes = { SheetsService.Scope.Spreadsheets };

        public SheetsService CreateSheetsService()
        {
            var credential = GoogleCredential.GetApplicationDefault().CreateScoped(_scopes);

            var sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
            });

            return sheetsService;
        }

        public SheetsService CreateSheetsService(SheetServiceOptions options)
        {
            var sheetsService = CreateSheetsService();
            sheetsService.HttpClient.Timeout = TimeSpan.FromSeconds(options.HttpClientTimeoutSeconds);
            return sheetsService;
        }
    }

    public record SheetServiceOptions
    {
        public int HttpClientTimeoutSeconds { get; init; } = 100;
    }
}
