﻿using AxoCover.Common.Extensions;
using AxoCover.Common.ProcessHost;
using AxoCover.Models.Data;
using AxoCover.Models.Data.CoverageReport;
using AxoCover.Models.Extensions;
using AxoCover.Properties;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AxoCover.Models
{
  public class AxoTestRunner : TestRunner
  {
    private const int _debuggerTimeout = 10000;
    private ExecutionProcess _executionProcess;
    private IEditorContext _editorContext;

    public AxoTestRunner(IEditorContext editorContext)
    {
      _editorContext = editorContext;
    }

    protected override TestReport RunTests(TestItem testItem, string testSettings, bool isCovering, bool isDebugging)
    {
      List<TestResult> testResults = new List<TestResult>();
      try
      {
        var testMethods = testItem
          .Flatten(p => p.Children)
          .OfType<Data.TestMethod>()
          .Where(p => p.Case != null)
          .ToArray();
        var testCases = testMethods
          .Select(p => p.Case)
          .ToArray();
        var testMethodsById = testMethods.ToDictionary(p => p.Case.Id);

        IHostProcessInfo hostProcessInfo = null;

        if (isCovering)
        {
          var solution = testItem.GetParent<TestSolution>();
          var coverageReportPath = Path.GetTempFileName();
          hostProcessInfo = new OpenCoverProcessInfo(solution.CodeAssemblies, solution.TestAssemblies, coverageReportPath);
        }

        _executionProcess = ExecutionProcess.Create(hostProcessInfo, Settings.Default.TestPlatform);
        _executionProcess.MessageReceived += (o, e) => OnTestLogAdded(e.Value);
        _executionProcess.TestStarted += (o, e) => OnTestStarted(testMethodsById[e.Value.Id]);
        _executionProcess.TestResult += (o, e) =>
        {
          var testResult = e.Value.ToTestResult(testMethodsById[e.Value.TestCase.Id]);
          testResults.Add(testResult);
          OnTestExecuted(testResult);
        };

        if (isDebugging)
        {
          OnTestLogAdded(Resources.DebuggerAttaching);
          if (_editorContext.AttachToProcess(_executionProcess.ProcessId) &&
            _executionProcess.WaitForDebugger(_debuggerTimeout))
          {
            OnTestLogAdded(Resources.DebuggerAttached);
            OnDebuggingStarted();
          }
          else
          {
            OnTestLogAdded(Resources.DebuggerFailedToAttach);
          }
        }

        try
        {
          _executionProcess.RunTests(testCases, testSettings, Settings.Default.TestApartmentState);
          _executionProcess.Shutdown();
        }
        catch
        {
          if (!isDebugging) throw;
        }

        if (isDebugging)
        {
          _editorContext.WaitForDetach();
          OnTestLogAdded(Resources.DebuggerDetached);
        }

        if (isCovering)
        {
          OnTestLogAdded(Resources.GeneratingCoverageReport);
        }

        _executionProcess.WaitForExit();

        if (_isAborting) return null;

        if (isCovering)
        {
          var coverageReportPath = (hostProcessInfo as OpenCoverProcessInfo).CoverageReportPath;
          if (System.IO.File.Exists(coverageReportPath))
          {
            var coverageReport = GenericExtensions.ParseXml<CoverageSession>(coverageReportPath);
            return new TestReport(testResults, coverageReport);
          }
        }
        else
        {
          return new TestReport(testResults, null);
        }
      }
      finally
      {
        if (_executionProcess != null)
        {
          _executionProcess.Dispose();
          _executionProcess = null;
        }
      }

      return null;
    }

    protected override void AbortTests()
    {
      if (_executionProcess != null)
      {
        _executionProcess.Dispose();
      }
    }
  }
}