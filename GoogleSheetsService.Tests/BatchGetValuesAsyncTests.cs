using Google;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace GoogleSheetsService.Tests
{
    public class BatchGetValuesAsyncTests
    {
        private readonly Mock<ISheetsServiceWrapper> _mockWrapper;
        private readonly Mock<ILogger> _mockLogger;
        private readonly GoogleSheetsService _service;

        public BatchGetValuesAsyncTests()
        {
            _mockWrapper = new Mock<ISheetsServiceWrapper>();
            _mockLogger = new Mock<ILogger>();
            _service = new GoogleSheetsService(_mockLogger.Object, _mockWrapper.Object, Mock.Of<ITimeProvider>());
        }

        [Fact]
        public async Task BatchGetValuesAsync_WithEmptyRanges_ReturnsNull()
        {
            // Act
            var result = await _service.BatchGetValuesAsync("spreadsheet-id", Array.Empty<string>());

            // Assert
            Assert.Null(result);
            _mockWrapper.Verify(w => w.BatchGetValuesAsync(It.IsAny<string>(), It.IsAny<IList<string>>()), Times.Never);
        }

        [Fact]
        public async Task BatchGetValuesAsync_WithValidRanges_MapsResponseToRequestedKeys()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var ranges = new[] { "Sheet1!A1:Z", "Sheet 2!A1:C" };

            var response = new BatchGetValuesResponse
            {
                ValueRanges = new List<ValueRange>
                {
                    new ValueRange { Range = "Sheet1!A1:Z", Values = new List<IList<object>> { new List<object> { "A" } } },
                    new ValueRange { Range = "'Sheet 2'!A1:C", Values = new List<IList<object>> { new List<object> { "B" } } }
                }
            };

            _mockWrapper
                .Setup(w => w.BatchGetValuesAsync(spreadsheetId, It.IsAny<IList<string>>()))
                .ReturnsAsync(response);

            // Act
            var result = await _service.BatchGetValuesAsync(spreadsheetId, ranges);

            // Assert
            Assert.Equal("A", result[ranges[0]][0][0]);
            Assert.Equal("B", result[ranges[1]][0][0]);
        }

        [Fact]
        public async Task BatchGetValuesAsync_WhenBadRequest_ReturnsNull()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var ranges = new[] { "Sheet1!A1:Z", "Sheet2!A1:C" };

            _mockWrapper
                .Setup(w => w.BatchGetValuesAsync(spreadsheetId, It.IsAny<IList<string>>()))
                .Throws(new GoogleApiException("service", "Bad Request") { HttpStatusCode = HttpStatusCode.BadRequest });

            // Act
            var result = await _service.BatchGetValuesAsync(spreadsheetId, ranges);

            // Assert
            Assert.Null(result);
        }
    }
}
