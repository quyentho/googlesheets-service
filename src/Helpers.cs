using System.Text;

internal static class Helpers
{

    private const int NumOfEnglishLetters = 26;
    public static string GetColumnFromValues(IList<IList<object>> values)
    {
        var columns = values[0].Count;
        if (columns > 52)
        {
            throw new ArgumentException("Columns greater than 52 are not supported");
        }

        string googleColumn = string.Empty;


        if (columns <= NumOfEnglishLetters)
        {
            googleColumn = GetUpperCaseCharacterByNumber(columns).ToString();
        }
        else
        {
            var googleSheetColumnSb = new StringBuilder("A");

            googleSheetColumnSb.Append(GetUpperCaseCharacterByNumber(columns - NumOfEnglishLetters));

            googleColumn = googleSheetColumnSb.ToString();
        }

        return googleColumn;
    }

    /// <summary>
    /// Map the number to the corresponding character
    /// E.g. 1 -> A, 2 -> B, 3 -> C, ..., 26 -> Z
    /// </summary>
    /// <param name="number"></param>
    /// <returns></returns>
    private static char GetUpperCaseCharacterByNumber(int number)
    {
        return (char)(64 + number);
    }
}