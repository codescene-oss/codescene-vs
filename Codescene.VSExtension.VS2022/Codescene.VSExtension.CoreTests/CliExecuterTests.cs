using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Tests
{
    [TestClass]
    public class CliExecuterTests
    {
        private readonly CliExecuter _cliExecuter;
        private readonly CliCommandProvider _cliCommandProvider;
        private readonly CliSettingsProvider _cliSettingsProvider;

        public CliExecuterTests()
        {
            _cliCommandProvider = new CliCommandProvider();
            _cliSettingsProvider = new CliSettingsProvider();
            _cliExecuter = new CliExecuter(_cliCommandProvider, _cliSettingsProvider);
        }


        [TestMethod]
        public void Test_Preflight()
        {
            var result = _cliExecuter.Preflight();
            Assert.IsNotNull(result);
        }
    }
}
