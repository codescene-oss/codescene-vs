//using Codescene.VSExtension.Core.Application.Services.Cli;
//using Codescene.VSExtension.Core.Application.Services.Mapper;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System.Linq;

//namespace Codescene.VSExtension.CoreTests
//{
//    [TestClass]
//    public class CliExecuterTests
//    {
//        private CliExecuter _executer;
//        [TestInitialize]
//        public void TestInitialize()
//        {
//            var commandProvider = new CliCommandProvider();
//            var mapper = new ModelMapper();
//            var settingsProvider = new CliSettingsProvider();
//            _executer = new CliExecuter(cliCommandProvider: commandProvider,
//                mapper: mapper, cliSettingsProvider: settingsProvider);
//        }

//        // Test for GetFileVersion.
//        [TestMethod]
//        public void TestReview()
//        {
//            var result = _executer.Review(@"C:\Users\User\source\repos\Codescene\vs-extensions-test\Codescene.VSExtension.VS2022\Codescene.VSExtension.CodeSmells\Issues\DeepGlobalNestedComplexityExample.cs");
//            Assert.IsTrue(result.FunctionLevel.Any());
//        }
//    }
//}
