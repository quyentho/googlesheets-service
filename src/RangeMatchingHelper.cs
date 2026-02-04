namespace GoogleSheetsService
{
    /// <summary>
    /// Helper class for Google Sheets range matching and normalization.
    /// Handles range transformations, key normalization, and matching logic.
    /// </summary>
    public static class RangeMatchingHelper
    {
        /// <summary>
        /// Finds the matching requested range key for a response range from Google Sheets API.
        /// Handles range expansion where "Sheet!A1:Z" becomes "Sheet!A1:Z999" in the response.
        /// </summary>
        /// <param name="requestedRanges">Array of originally requested ranges</param>
        /// <param name="responseRange">Range returned by Google Sheets API</param>
        /// <returns>The matching requested range key, or null if no match is found</returns>
        public static string? FindMatchingRangeKey(string[]? requestedRanges, string responseRange)
        {
            if (string.IsNullOrWhiteSpace(responseRange) || requestedRanges == null || requestedRanges.Length == 0)
            {
                return null;
            }

            var normalizedResponse = NormalizeRangeKey(responseRange);
            var responseStart = ExtractRangeStart(normalizedResponse);

            // Try exact match first
            for (int i = 0; i < requestedRanges.Length; i++)
            {
                var candidate = requestedRanges[i];
                var normalizedCandidate = NormalizeRangeKey(candidate);
                
                if (string.Equals(normalizedCandidate, normalizedResponse, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            // Fallback: match by sheet name + start position (ignores end row/column)
            // This handles cases where "Sheet!A1:Z" becomes "Sheet!A1:Z999"
            for (int i = 0; i < requestedRanges.Length; i++)
            {
                var candidate = requestedRanges[i];
                var normalizedCandidate = NormalizeRangeKey(candidate);
                var candidateStart = ExtractRangeStart(normalizedCandidate);
                
                if (!string.IsNullOrEmpty(candidateStart) && 
                    !string.IsNullOrEmpty(responseStart) &&
                    string.Equals(candidateStart, responseStart, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalizes a range key by removing quotes from sheet names.
        /// Handles quoted sheet names: 'Sheet Name'!A1:Z -> Sheet Name!A1:Z
        /// </summary>
        /// <param name="range">Range string that may contain quoted sheet name</param>
        /// <returns>Normalized range string without quotes</returns>
        public static string NormalizeRangeKey(string range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return string.Empty;
            }

            // Normalize quoted sheet names: 'Sheet Name'!A1:Z -> Sheet Name!A1:Z
            var trimmed = range.Trim();
            if (trimmed.StartsWith("'", StringComparison.Ordinal) && trimmed.Contains("'!"))
            {
                var endQuoteIndex = trimmed.IndexOf("'!", StringComparison.Ordinal);
                if (endQuoteIndex > 0)
                {
                    var sheetName = trimmed.Substring(1, endQuoteIndex - 1);
                    var rest = trimmed.Substring(endQuoteIndex + 2);
                    return $"{sheetName}!{rest}";
                }
            }

            return trimmed;
        }

        /// <summary>
        /// Extracts the sheet name and start cell from a range.
        /// Ignores the end column/row to focus on the range start position.
        /// </summary>
        /// <param name="range">Range string (e.g., "Sheet!A1:Z100")</param>
        /// <returns>Sheet name and start cell (e.g., "Sheet!A1")</returns>
        public static string ExtractRangeStart(string range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return string.Empty;
            }

            // Extract the part of the range before the last ':' if it exists
            var lastColonIndex = range.LastIndexOf(':');
            if (lastColonIndex > 0)
            {
                return range.Substring(0, lastColonIndex);
            }

            return range;
        }
    }
}
