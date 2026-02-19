using GoogleSheetsService.Resilience;
using Microsoft.Extensions.Logging;
using Moq;
using Polly;
using Xunit;

namespace GoogleSheetsService.Tests.Resilience
{
    /// <summary>
    /// Tests for the GoogleSheetsServiceWithResilience decorator.
    /// </summary>
    public class GoogleSheetsServiceWithResilienceTests
    {
        private readonly Mock<IGoogleSheetsService> _innerServiceMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly ResiliencePipeline _noOpPipeline;

        public GoogleSheetsServiceWithResilienceTests()
        {
            _innerServiceMock = new Mock<IGoogleSheetsService>();
            _loggerMock = new Mock<ILogger>();
            _noOpPipeline = ResiliencePipeline.Empty;
        }

        [Fact]
        public void Constructor_WithNullInnerService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GoogleSheetsServiceWithResilience(null!, _noOpPipeline, _loggerMock.Object)
            );
        }

        [Fact]
        public void Constructor_WithNullPolicy_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GoogleSheetsServiceWithResilience(_innerServiceMock.Object, null!, _loggerMock.Object)
            );
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GoogleSheetsServiceWithResilience(_innerServiceMock.Object, _noOpPipeline, null!)
            );
        }

        [Fact]
        public async Task ReadSheetAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var expectedData = new List<IList<object>>
            {
                new List<object> { "Header1", "Header2" },
                new List<object> { "Value1", "Value2" }
            };

            _innerServiceMock
                .Setup(s => s.ReadSheetAsync("spreadsheetId", "SheetName", "A1:Z100"))
                .ReturnsAsync(expectedData);

            // Act
            var result = await decorator.ReadSheetAsync("spreadsheetId", "SheetName", "A1:Z100");

            // Assert
            Assert.Equal(expectedData, result);
            _innerServiceMock.Verify(
                s => s.ReadSheetAsync("spreadsheetId", "SheetName", "A1:Z100"),
                Times.Once
            );
        }

        [Fact]
        public async Task WriteSheetAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var values = new List<IList<object>>
            {
                new List<object> { "Value1", "Value2" }
            };

            _innerServiceMock
                .Setup(s => s.WriteSheetAsync("spreadsheetId", "SheetName", "A1:B1", values))
                .Returns(Task.CompletedTask);

            // Act
            await decorator.WriteSheetAsync("spreadsheetId", "SheetName", "A1:B1", values);

            // Assert
            _innerServiceMock.Verify(
                s => s.WriteSheetAsync("spreadsheetId", "SheetName", "A1:B1", values),
                Times.Once
            );
        }

        [Fact]
        public async Task ReadSheetInChunksAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var expectedData = new List<IList<object>>();
            _innerServiceMock
                .Setup(s => s.ReadSheetInChunksAsync("id", "Sheet", "A1:Z", 1000))
                .ReturnsAsync(expectedData);

            // Act
            var result = await decorator.ReadSheetInChunksAsync("id", "Sheet", "A1:Z", 1000);

            // Assert
            Assert.Equal(expectedData, result);
        }

        [Fact]
        public async Task BatchGetValuesAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var ranges = new[] { "Sheet1!A1:Z", "Sheet2!A1:C" };
            var expectedData = new Dictionary<string, IList<IList<object>>>();
            _innerServiceMock
                .Setup(s => s.BatchGetValuesAsync("id", ranges))
                .ReturnsAsync(expectedData);

            // Act
            var result = await decorator.BatchGetValuesAsync("id", ranges);

            // Assert
            Assert.Equal(expectedData, result);
        }

        [Fact]
        public async Task AppendToEndAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var values = new List<IList<object>>();
            _innerServiceMock
                .Setup(s => s.AppendToEndAsync("id", "Sheet", values, "A:Z"))
                .Returns(Task.CompletedTask);

            // Act
            await decorator.AppendToEndAsync("id", "Sheet", values, "A:Z");

            // Assert
            _innerServiceMock.Verify(
                s => s.AppendToEndAsync("id", "Sheet", values, "A:Z"),
                Times.Once
            );
        }

        [Fact]
        public async Task AllMethods_PropagateExceptions()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var exception = new Exception("Test exception");

            _innerServiceMock
                .Setup(s => s.ReadSheetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                decorator.ReadSheetAsync("id", "Sheet", "A1:Z")
            );
        }

        [Fact]
        public async Task ReplaceFromSecondRowAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            var values = new List<IList<object>>();
            _innerServiceMock
                .Setup(s => s.ReplaceFromSecondRowAsync("id", "Sheet", values))
                .Returns(Task.CompletedTask);

            // Act
            await decorator.ReplaceFromSecondRowAsync("id", "Sheet", values);

            // Assert
            _innerServiceMock.Verify(
                s => s.ReplaceFromSecondRowAsync("id", "Sheet", values),
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteRowsAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            _innerServiceMock
                .Setup(s => s.DeleteRowsAsync("id", "Sheet", 5))
                .Returns(Task.CompletedTask);

            // Act
            await decorator.DeleteRowsAsync("id", "Sheet", 5);

            // Assert
            _innerServiceMock.Verify(
                s => s.DeleteRowsAsync("id", "Sheet", 5),
                Times.Once
            );
        }

        [Fact]
        public async Task AddSheetAsync_CallsInnerServiceWithPolicy()
        {
            // Arrange
            var decorator = new GoogleSheetsServiceWithResilience(
                _innerServiceMock.Object,
                _noOpPipeline,
                _loggerMock.Object
            );

            _innerServiceMock
                .Setup(s => s.AddSheetAsync("id", "NewSheet"))
                .Returns(Task.CompletedTask);

            // Act
            await decorator.AddSheetAsync("id", "NewSheet");

            // Assert
            _innerServiceMock.Verify(
                s => s.AddSheetAsync("id", "NewSheet"),
                Times.Once
            );
        }
    }
}
