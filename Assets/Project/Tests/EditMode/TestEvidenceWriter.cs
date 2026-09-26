using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace Sokoban.Tests.EditMode
{
    // The CLI reports JSON; keep the original Unity/NUnit XML for each requested run too.
    [InitializeOnLoad]
    internal sealed class TestEvidenceWriter : ICallbacks
    {
        private const string OutputKey = "Sokoban.TestEvidencePath";

        static TestEvidenceWriter()
        {
            TestRunnerApi.RegisterTestCallback(new TestEvidenceWriter());
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            string path = SessionState.GetString(OutputKey, "");
            if (string.IsNullOrEmpty(path)) return;
            SessionState.EraseString(OutputKey);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            TestRunnerApi.SaveResultToFile(result, path);
        }
    }
}
