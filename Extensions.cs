namespace GoogletSheetsService
{
    public static class Extensions    
    {
        public static IList<IList<object>> ToGoogleSheetsValues<T>(this IEnumerable<T> list)
        {
            // Get the properties of the class
            var properties = typeof(T).GetProperties();

            // Convert the list of objects to an IList<IList<object>> using reflection
            return list.Select(p => properties.Select(prop => prop.GetValue(p)).ToList()).Cast<IList<object>>().ToList();
        }
    }
}
