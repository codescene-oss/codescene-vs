window.BENCHMARK_DATA = {
  "lastUpdate": 1774973756414,
  "repoUrl": "https://github.com/codescene-oss/codescene-vs",
  "entries": {
    "Code Review - CliExecutor": [
      {
        "commit": {
          "author": {
            "name": "codescene-oss",
            "username": "codescene-oss"
          },
          "committer": {
            "name": "codescene-oss",
            "username": "codescene-oss"
          },
          "id": "0ea86bd7ca3a6c56aae89e61ff2e9ee49460cce3",
          "message": "chore: benchmark tests",
          "timestamp": "2026-03-31T12:13:31Z",
          "url": "https://github.com/codescene-oss/codescene-vs/pull/271/commits/0ea86bd7ca3a6c56aae89e61ff2e9ee49460cce3"
        },
        "date": 1774973577608,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewContentAsync",
            "value": 140726723.07692307,
            "unit": "ns",
            "range": "± 823826.0569631251"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewDeltaAsync",
            "value": 128893605.76923077,
            "unit": "ns",
            "range": "± 3492167.1784229716"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.FnsToRefactorFromDeltaAsync",
            "value": 57276342.222222224,
            "unit": "ns",
            "range": "± 359140.9099690585"
          }
        ]
      }
    ],
    "Code Review - CodeReviewer": [
      {
        "commit": {
          "author": {
            "name": "codescene-oss",
            "username": "codescene-oss"
          },
          "committer": {
            "name": "codescene-oss",
            "username": "codescene-oss"
          },
          "id": "0ea86bd7ca3a6c56aae89e61ff2e9ee49460cce3",
          "message": "chore: benchmark tests",
          "timestamp": "2026-03-31T12:13:31Z",
          "url": "https://github.com/codescene-oss/codescene-vs/pull/271/commits/0ea86bd7ca3a6c56aae89e61ff2e9ee49460cce3"
        },
        "date": 1774973754855,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewAsync",
            "value": 155028571.66666666,
            "unit": "ns",
            "range": "± 1033658.1830388106"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.GetOrComputeBaselineRawScoreAsync",
            "value": 144951501.92307693,
            "unit": "ns",
            "range": "± 1286170.319546136"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewAndBaselineAsync",
            "value": 300209330,
            "unit": "ns",
            "range": "± 2227886.195862411"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.DeltaAsync",
            "value": 270514289.28571427,
            "unit": "ns",
            "range": "± 1640106.9852743156"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewWithDeltaAsync",
            "value": 427376121.4285714,
            "unit": "ns",
            "range": "± 2521630.038495726"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.DeltaAsyncWithRefactorDiscovery",
            "value": 337015620,
            "unit": "ns",
            "range": "± 2201885.5433468833"
          }
        ]
      }
    ]
  }
}