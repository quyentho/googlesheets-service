using System.Collections;

namespace GoogleSheetsService.Tests
{
    public class SingleColumnTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            for (int i = 1; i <= 26; i++)
            {
                yield return new object[] { i, ((char)(64 + i)).ToString() };
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    public class HelpersTests
    {

        [Theory]
        [ClassData(typeof(SingleColumnTestData))]
        public void GetColumnFromValues_ShouldReturnCorrectColumn_ForSingleColumn(int columnIndex, string expectedColumn)
        {
            // Arrange
            var values = new List<IList<object>>
            {
                new List<object>(new object[columnIndex])
            };

            // Act
            var result = Helpers.GetColumnFromValues(values);

            // Assert
            Assert.Equal(expectedColumn, result);
        }

        [Fact]
        public void GetColumnFromValues_ShouldReturnCorrectColumn_ForColumnsBetween27And52()
        {
            // Arrange
            var values = new List<IList<object>>
            {
                 new object[27]
            };

            // Act
            var result = Helpers.GetColumnFromValues(values);

            // Assert
            Assert.Equal("AA", result);
        }
    }
}
