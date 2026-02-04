using System.Reflection;

namespace GoogleSheetsService.Tests
{
    /// <summary>
    /// Comprehensive tests for range matching logic in GoogleSheetsService.
    /// Tests the RangeMatchingHelper class and its range matching utilities.
    /// </summary>
    public class RangeMatchingTests
    {
        #region FindMatchingRangeKey Tests

        /// <summary>
        /// Tests exact match with simple range format.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnKey_WhenExactMatchExists()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!A1:Z100" };
            var responseRange = "Sheet1!A1:Z100";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Sheet1!A1:Z100", result);
        }

        /// <summary>
        /// Tests case-insensitive matching for sheet names and ranges.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnKey_WhenCaseInsensitiveMatch()
        {
            // Arrange
            var requestedRanges = new[] { "sheet1!a1:z100" };
            var responseRange = "Sheet1!A1:Z100";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("sheet1!a1:z100", result);
        }

        /// <summary>
        /// Tests range expansion scenario: open-ended range becomes fixed range.
        /// This is the primary bug fix - Google returns "Sheet!A1:Z999" when we request "Sheet!A1:Z"
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldMatchOpenEndedRange_WhenGoogleExpandsRange()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!A1:Z" };  // Open-ended range
            var responseRange = "Sheet1!A1:Z999";           // Google expands it

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Sheet1!A1:Z", result);
        }

        /// <summary>
        /// Tests range expansion with different column ranges.
        /// </summary>
        [Theory]
        [InlineData("Sheet1!B1:Z", "Sheet1!B1:Z500")]
        [InlineData("Sheet1!A2:AI", "Sheet1!A2:AI999")]
        [InlineData("Sheet1!B1:C", "Sheet1!B1:C1500")]
        public void FindMatchingRangeKey_ShouldMatchExpandedRanges(string requested, string response)
        {
            // Arrange
            var requestedRanges = new[] { requested };

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, response);

            // Assert
            Assert.Equal(requested, result);
        }

        /// <summary>
        /// Tests matching with quoted sheet names (special characters in sheet name).
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldMatch_WhenSheetNameHasSpecialCharacters()
        {
            // Arrange
            var requestedRanges = new[] { "'Dán Link File'!B1:Z" };
            var responseRange = "'Dán Link File'!B1:Z999";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("'Dán Link File'!B1:Z", result);
        }

        /// <summary>
        /// Tests matching with quoted vs unquoted sheet names (normalization).
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldMatch_WhenQuotesAreInconsistent()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet Name!A1:Z" };
            var responseRange = "'Sheet Name'!A1:Z999";  // API adds quotes

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Sheet Name!A1:Z", result);
        }

        /// <summary>
        /// Tests multiple requested ranges, should match the correct one.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnCorrectKey_WhenMultipleRangesRequested()
        {
            // Arrange
            var requestedRanges = new[]
            {
                "Sheet1!A1:Z",
                "Sheet2!A1:C",
                "Sheet3!D1:F100"
            };
            var responseRange = "Sheet2!A1:C500";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Sheet2!A1:C", result);
        }

        /// <summary>
        /// Tests with similar range patterns to ensure correct matching.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldMatchCorrectRange_WithSimilarPatterns()
        {
            // Arrange
            var requestedRanges = new[]
            {
                "Data!A1:Z",      // Requested
                "Data!A1:AA",     // Similar but different
                "Data!B1:Z"       // Different start column
            };
            var responseRange = "Data!A1:Z999";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Data!A1:Z", result);
        }

        /// <summary>
        /// Tests matching with column ranges that have multi-letter columns.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldMatch_WithMultiLetterColumns()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!AA1:ZZ" };
            var responseRange = "Sheet1!AA1:ZZ999";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Sheet1!AA1:ZZ", result);
        }

        /// <summary>
        /// Tests that null is returned when no match found.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnNull_WhenNoMatchFound()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!A1:Z" };
            var responseRange = "Sheet2!A1:Z999";  // Different sheet

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests with empty requested ranges array.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnNull_WhenRequestedRangesEmpty()
        {
            // Arrange
            var requestedRanges = Array.Empty<string>();
            var responseRange = "Sheet1!A1:Z999";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests with null requested ranges.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnNull_WhenRequestedRangesNull()
        {
            // Arrange
            string[]? requestedRanges = null;
            var responseRange = "Sheet1!A1:Z999";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests with empty response range.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnNull_WhenResponseRangeEmpty()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!A1:Z" };
            var responseRange = string.Empty;

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests with whitespace response range.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_ShouldReturnNull_WhenResponseRangeWhitespace()
        {
            // Arrange
            var requestedRanges = new[] { "Sheet1!A1:Z" };
            var responseRange = "   ";

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region NormalizeRangeKey Tests

        /// <summary>
        /// Tests that unquoted ranges remain unchanged.
        /// </summary>
        [Fact]
        public void NormalizeRangeKey_ShouldReturnUnchanged_ForUnquotedRange()
        {
            // Arrange
            var range = "Sheet1!A1:Z100";

            // Act
            var result = RangeMatchingHelper.NormalizeRangeKey(range);

            // Assert
            Assert.Equal("Sheet1!A1:Z100", result);
        }

        /// <summary>
        /// Tests that quoted sheet names are unquoted.
        /// </summary>
        [Fact]
        public void NormalizeRangeKey_ShouldRemoveQuotes_ForQuotedSheetName()
        {
            // Arrange
            var range = "'Sheet Name'!A1:Z100";

            // Act
            var result = RangeMatchingHelper.NormalizeRangeKey(range);

            // Assert
            Assert.Equal("Sheet Name!A1:Z100", result);
        }

        /// <summary>
        /// Tests normalization with special characters in sheet name.
        /// </summary>
        [Fact]
        public void NormalizeRangeKey_ShouldHandleSpecialCharacters_InSheetName()
        {
            // Arrange
            var range = "'Dán Link File'!B1:Z";

            // Act
            var result = RangeMatchingHelper.NormalizeRangeKey(range);

            // Assert
            Assert.Equal("Dán Link File!B1:Z", result);
        }

        /// <summary>
        /// Tests with empty string.
        /// </summary>
        [Fact]
        public void NormalizeRangeKey_ShouldReturnEmpty_ForEmptyString()
        {
            // Arrange
            var range = string.Empty;

            // Act
            var result = RangeMatchingHelper.NormalizeRangeKey(range);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests with whitespace.
        /// </summary>
        [Fact]
        public void NormalizeRangeKey_ShouldReturnEmpty_ForWhitespaceOnly()
        {
            // Arrange
            var range = "   ";

            // Act
            var result = RangeMatchingHelper.NormalizeRangeKey(range);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region ExtractRangeStart Tests

        /// <summary>
        /// Tests extraction of range start (sheet + start cell).
        /// </summary>
        [Theory]
        [InlineData("Sheet1!A1:Z100", "Sheet1!A1")]
        [InlineData("Sheet1!B2:Z", "Sheet1!B2")]
        [InlineData("Sheet!A1:AA", "Sheet!A1")]
        [InlineData("Data!AA1:ZZ999", "Data!AA1")]
        public void ExtractRangeStart_ShouldExtractCorrectly(string range, string expected)
        {
            // Act
            var result = RangeMatchingHelper.ExtractRangeStart(range);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests with range without colon (single cell).
        /// </summary>
        [Fact]
        public void ExtractRangeStart_ShouldReturnRange_WhenNoColonPresent()
        {
            // Arrange
            var range = "Sheet1!A1";

            // Act
            var result = RangeMatchingHelper.ExtractRangeStart(range);

            // Assert
            Assert.Equal("Sheet1!A1", result);
        }

        /// <summary>
        /// Tests with empty string.
        /// </summary>
        [Fact]
        public void ExtractRangeStart_ShouldReturnEmpty_ForEmptyString()
        {
            // Arrange
            var range = string.Empty;

            // Act
            var result = RangeMatchingHelper.ExtractRangeStart(range);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Real-world scenario: batch request with multiple ranges from same spreadsheet.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_RealWorldScenario_MultipleRangesBatchRead()
        {
            // Arrange - Simulating the exact scenario from the bug report
            var requestedRanges = new[]
            {
                "Dán Link File!B1:Z",      // Open-ended range
                "Tổng quan!A1:C"           // Open-ended range
            };

            // Google returns expanded ranges
            var responses = new[] 
            { 
                "Dán Link File!B1:Z999",   // Expanded
                "Tổng quan!A1:C50"         // Expanded
            };

            // Act & Assert - Each response should match its original request
            var result1 = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responses[0]);
            var result2 = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responses[1]);

            Assert.Equal("Dán Link File!B1:Z", result1);
            Assert.Equal("Tổng quan!A1:C", result2);
        }

        /// <summary>
        /// Scenario where API adds quotes to sheet names with spaces/special chars.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_RealWorldScenario_QuotingInclusion()
        {
            // Arrange
            var requestedRanges = new[] { "Dán Link File!B1:Z" };
            var responseRange = "'Dán Link File'!B1:Z999";  // API added quotes

            // Act
            var result = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, responseRange);

            // Assert
            Assert.Equal("Dán Link File!B1:Z", result);
        }

        /// <summary>
        /// Scenario with fulfillment data ranges.
        /// </summary>
        [Fact]
        public void FindMatchingRangeKey_RealWorldScenario_FulfillmentDataRanges()
        {
            // Arrange
            var requestedRanges = new[]
            {
                "Sheet1!A2:AI",
                "Sheet2!A2:AI",
                "Sheet3!A2:AI"
            };

            // Act & Assert
            var result1 = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, "Sheet1!A2:AI1000");
            var result2 = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, "Sheet2!A2:AI500");
            var result3 = RangeMatchingHelper.FindMatchingRangeKey(requestedRanges, "Sheet3!A2:AI50");

            Assert.Equal("Sheet1!A2:AI", result1);
            Assert.Equal("Sheet2!A2:AI", result2);
            Assert.Equal("Sheet3!A2:AI", result3);
        }

        #endregion
    }
}
