namespace MRE.Tests.Unit
{
    public class MyBusinessTests
    {
        [Theory]
        [InlineData(null, "maybe")]
        [InlineData(true, "yes")]
        [InlineData(false, "no")]
        public void GetDisplayValue_Test(bool? val, string expected)
        {
            // Arrange
            var myBusiness = new MyBusiness();

            // Act
            var result = myBusiness.GetDisplayValue(val);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}