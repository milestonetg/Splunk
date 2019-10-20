using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MilestoneTG.Extensions.Logging.Splunk.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            ILogger logger = SplunkLogger.GetLogger("test", new SplunkLoggerOptions());

            logger.LogInformation("test");
        }
    }
}
