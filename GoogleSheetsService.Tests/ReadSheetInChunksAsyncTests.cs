using Google;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace GoogleSheetsService.Tests
{
    public class ReadSheetInChunksAsyncTests
    {
        private readonly Mock<ISheetsServiceWrapper> _mockWrapper;
        private readonly Mock<ILogger> _mockLogger;
        private readonly GoogleSheetsService _service;

        public ReadSheetInChunksAsyncTests()
        {
            _mockWrapper = new Mock<ISheetsServiceWrapper>();
            _mockLogger = new Mock<ILogger>();
            _service = new GoogleSheetsService(_mockLogger.Object, _mockWrapper.Object);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithValidRange_ReturnsAccumulatedRows()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            var rows = new List<IList<object>>
            {
                new List<object> { "row1col1", "row1col2" },
                new List<object> { "row2col1", "row2col2" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("row1col1", result[0][0]);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithMultiLetterColumns_ParsesCorrectly()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "AA2:AI";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" },
                new List<object> { "value2" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!AA2:AI1001"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithMultiDigitStartRow_ParsesCorrectly()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A100:Z";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A100:Z1099"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithEmptyResponse_ReturnsNull()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .ReturnsAsync(new ValueRange { Values = null });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithEmptyListResponse_ReturnsNull()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .ReturnsAsync(new ValueRange { Values = new List<IList<object>>() });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithMultipleChunks_AccumulatesAllRows()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";
            var chunkSize = 2;

            var chunk1 = new List<IList<object>>
            {
                new List<object> { "row1" },
                new List<object> { "row2" }
            };

            var chunk2 = new List<IList<object>>
            {
                new List<object> { "row3" },
                new List<object> { "row4" }
            };

            var chunk3 = new List<IList<object>>
            {
                new List<object> { "row5" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z3"))
                .ReturnsAsync(new ValueRange { Values = chunk1 });
            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A4:Z5"))
                .ReturnsAsync(new ValueRange { Values = chunk2 });
            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A6:Z7"))
                .ReturnsAsync(new ValueRange { Values = chunk3 });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range, chunkSize);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            _mockWrapper.Verify(x => x.GetValuesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithTooManyRequestsException_RetriesAfterDelay()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" }
            };

            var callCount = 0;
            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new GoogleApiException("service", "Too many requests")
                        {
                            HttpStatusCode = HttpStatusCode.TooManyRequests
                        };
                    }
                    return Task.FromResult<ValueRange?>(new ValueRange { Values = rows });
                });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Too many requests")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithBadRequestException_ReturnsPartialResults()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";
            var chunkSize = 2;

            var chunk1 = new List<IList<object>>
            {
                new List<object> { "row1" },
                new List<object> { "row2" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z3"))
                .ReturnsAsync(new ValueRange { Values = chunk1 });
            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A4:Z5"))
                .Throws(new GoogleApiException("service", "Bad Request") { HttpStatusCode = HttpStatusCode.BadRequest });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range, chunkSize);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithBadRequestOnFirstChunk_ReturnsNull()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .Throws(new GoogleApiException("service", "Bad Request") { HttpStatusCode = HttpStatusCode.BadRequest });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithInvalidRange_FallsBackToSingleRead()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var invalidRange = "InvalidRange";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!InvalidRange"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, invalidRange);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithFewerRowsThanChunkSize_TerminatesLoop()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            var rows = new List<IList<object>>
            {
                new List<object> { "row1" },
                new List<object> { "row2" },
                new List<object> { "row3" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            _mockWrapper.Verify(x => x.GetValuesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithComplexMultiLetterColumns_ParsesCorrectly()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "AAA1000:ZZZ";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!AAA1000:ZZZ1999"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithNoStartRow_DefaultsToRow1()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A:Z";

            var rows = new List<IList<object>>
            {
                new List<object> { "value1" }
            };

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A1:Z1000"))
                .ReturnsAsync(new ValueRange { Values = rows });

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_WithNullResponse_ReturnsNull()
        {
            // Arrange
            var spreadsheetId = "test-id";
            var sheetName = "Sheet1";
            var range = "A2:Z";

            _mockWrapper.Setup(x => x.GetValuesAsync(spreadsheetId, "Sheet1!A2:Z1001"))
                .ReturnsAsync((ValueRange?)null);

            // Act
            var result = await _service.ReadSheetInChunksAsync(spreadsheetId, sheetName, range);

            // Assert
            Assert.Null(result);
        }
    }
}
