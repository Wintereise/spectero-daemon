using Newtonsoft.Json;
using NUnit.Framework;
using Spectero.daemon.Libraries.APM;
using Assert = NUnit.Framework.Assert;

namespace daemon_testcases
{
    /*
     * Application Performance Management
     * This class is dedicated to testing the serialization of the host's operating system dictionary set.
     */

    [TestFixture]
    public class ApmUnitTest
    {
        [Test]
        public void IsSerializable()
        {
            // Instantiate new APM
            Apm localApm = new Apm();

            // Get the details we need
            var details = localApm.GetAllDetails();

            // Here's what matters, can we serialize it?
            JsonConvert.SerializeObject(details, Formatting.Indented);
        }
    }
}