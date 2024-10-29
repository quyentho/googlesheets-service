using Google.Apis.Sheets.v4;

namespace GoogleSheetsService
{
    public interface ISheetServiceFactory
    {
        SheetsService CreateSheetsService();
        SheetsService CreateSheetsService(SheetServiceOptions options);
    }
}