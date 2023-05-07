# googlesheets-service
Simple service to read and write to google sheets using `Google.Apis.Sheets.v4`
# How to setup:
1. Create a [google cloud project](https://console.cloud.google.com/).
2. Enable Google sheets API for the project
3. Create a service account
4. Create a key for the service account and download as JSON
5. Place the JSON file with name `client_secrets.json` in the project directory (copy if newer)
6. Copy the email of the service account -> go to the google sheets you want to use -> share this sheets with the service account email
