# googlesheets-service
Simple service to read and write to google sheets using `Google.Apis.Sheets.v4`
# How to setup:
1. Create a [google cloud project](https://console.cloud.google.com/).
2. Enable Google sheets API for the project
3. Create a service account
4. Create a key for the service account and download as JSON
5. Set environment variable `GOOGLE_APPLICATION_CREDENTIALS` to the path of the JSON file
1. 6. Copy the email of the service account -> go to the google sheets you want to use -> share this sheets with the service account email
# Usage example:

## Read:
```c#
// See https://aka.ms/new-console-template for more information
using GoogletSheetsService;
using Newtonsoft.Json;
using System;

var sheetsService = new GoogleSheetsService();

// Read data from the sheet
// How to get sheet id: https://developers.google.com/sheets/api/guides/concepts
var spreadsheetId = "<your sheet ID>";
var sheetName = "Sheet1";
var range = "A2:B2";
var data = await sheetsService.ReadSheetAsync(spreadsheetId, sheetName, range);

var json = JsonConvert.SerializeObject(data);
Console.WriteLine(json);
```
## Write:
```c#
// Write data
var values = new List<IList<object>>
{
    new List<object> { "Name", "Age" },
    new List<object> { "Alice", 25 },
    new List<object> { "Bob", 30 }
};

await sheetsService.WriteSheetAsync(spreadsheetId, sheetName, "A1:B3", values);
```
## Write at last empty row:
```c#
// Create a list of lists of objects to write to the sheet
var people = new List<Person>
{
    new Person (  "Alice",  25,  "123 Main St" ),
    new Person ("Bob", 30, "456 Oak Ave"),
    new Person ("Another Person", 30, "456 Oak Ave")
};


// Convert the list of Person objects to an IList<IList<object>> using reflection
var values = people.ToGoogleSheetsValues();

await sheetsService.WriteSheetAtLastRowAsync(spreadsheetId, sheetName, values);

public record Person(string Name, int Age, string Address);
```
