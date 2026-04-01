window.BENCHMARK_DATA = {
  "lastUpdate": 1775029645981,
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
      },
      {
        "commit": {
          "author": {
            "email": "martin.safsten@codescene.com",
            "name": "Martin Säfsten",
            "username": "martinsafsten-codescene"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "eb37471cdc2d9ce6494c25f8c146d0122bd85b45",
          "message": "chore: benchmark tests (#271)",
          "timestamp": "2026-03-31T18:20:25+02:00",
          "tree_id": "1e04142bc5acb6e6129fabdeb92dd083fee39df3",
          "url": "https://github.com/codescene-oss/codescene-vs/commit/eb37471cdc2d9ce6494c25f8c146d0122bd85b45"
        },
        "date": 1774974282559,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewContentAsync",
            "value": 146109010.29411766,
            "unit": "ns",
            "range": "± 2225601.1451975387"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewDeltaAsync",
            "value": 134181233.33333333,
            "unit": "ns",
            "range": "± 1727540.3266025018"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.FnsToRefactorFromDeltaAsync",
            "value": 57482012.698412694,
            "unit": "ns",
            "range": "± 185681.0663014717"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "martin.safsten@codescene.com",
            "name": "Martin Säfsten",
            "username": "martinsafsten-codescene"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a75630a362370d91945c8cf0ba983c39c75483bd",
          "message": "chore: add alerting options to benchmark workflows",
          "timestamp": "2026-04-01T09:42:24+02:00",
          "tree_id": "8759ffac82074d2803ed390860db1fca51f55c4b",
          "url": "https://github.com/codescene-oss/codescene-vs/commit/a75630a362370d91945c8cf0ba983c39c75483bd"
        },
        "date": 1775029642603,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewContentAsync",
            "value": 143344644.5,
            "unit": "ns",
            "range": "± 11135917.54730774"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.ReviewDeltaAsync",
            "value": 123809094.28571428,
            "unit": "ns",
            "range": "± 293913.86832812836"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CliExecutorBenchmarks.FnsToRefactorFromDeltaAsync",
            "value": 59286389.23076923,
            "unit": "ns",
            "range": "± 670214.1830348158"
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
      },
      {
        "commit": {
          "author": {
            "email": "martin.safsten@codescene.com",
            "name": "Martin Säfsten",
            "username": "martinsafsten-codescene"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "eb37471cdc2d9ce6494c25f8c146d0122bd85b45",
          "message": "chore: benchmark tests (#271)",
          "timestamp": "2026-03-31T18:20:25+02:00",
          "tree_id": "1e04142bc5acb6e6129fabdeb92dd083fee39df3",
          "url": "https://github.com/codescene-oss/codescene-vs/commit/eb37471cdc2d9ce6494c25f8c146d0122bd85b45"
        },
        "date": 1774974476292,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewAsync",
            "value": 157509658.92857143,
            "unit": "ns",
            "range": "± 836448.2853115826"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.GetOrComputeBaselineRawScoreAsync",
            "value": 147701089.2857143,
            "unit": "ns",
            "range": "± 1928540.4147236657"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewAndBaselineAsync",
            "value": 301094366.6666667,
            "unit": "ns",
            "range": "± 2874522.6945198267"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.DeltaAsync",
            "value": 271710140,
            "unit": "ns",
            "range": "± 3105792.6718932344"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.ReviewWithDeltaAsync",
            "value": 428635300,
            "unit": "ns",
            "range": "± 4337423.727903998"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CodeReviewerBenchmarks.DeltaAsyncWithRefactorDiscovery",
            "value": 333867000,
            "unit": "ns",
            "range": "± 3272858.2663214384"
          }
        ]
      }
    ],
    "Code Review - CachingCodeReviewer": [
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
        "date": 1774973858700,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewAsyncCold",
            "value": 155616431.66666666,
            "unit": "ns",
            "range": "± 781358.0790650585"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewAsyncWarm",
            "value": 6411.121490478516,
            "unit": "ns",
            "range": "± 170.31843705296478"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewWithDeltaAsyncCold",
            "value": 505900986.20689654,
            "unit": "ns",
            "range": "± 14716139.191055419"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewWithDeltaAsyncWarm",
            "value": 21805.803629557293,
            "unit": "ns",
            "range": "± 291.8361732523888"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "martin.safsten@codescene.com",
            "name": "Martin Säfsten",
            "username": "martinsafsten-codescene"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "eb37471cdc2d9ce6494c25f8c146d0122bd85b45",
          "message": "chore: benchmark tests (#271)",
          "timestamp": "2026-03-31T18:20:25+02:00",
          "tree_id": "1e04142bc5acb6e6129fabdeb92dd083fee39df3",
          "url": "https://github.com/codescene-oss/codescene-vs/commit/eb37471cdc2d9ce6494c25f8c146d0122bd85b45"
        },
        "date": 1774974592712,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewAsyncCold",
            "value": 155147614.28571427,
            "unit": "ns",
            "range": "± 2582994.771504396"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewAsyncWarm",
            "value": 5323.954990931919,
            "unit": "ns",
            "range": "± 19.9635551231549"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewWithDeltaAsyncCold",
            "value": 515832053.3333333,
            "unit": "ns",
            "range": "± 17132465.773039315"
          },
          {
            "name": "Codescene.VSExtension.Core.Benchmarks.CachingCodeReviewerBenchmarks.ReviewWithDeltaAsyncWarm",
            "value": 19819.48214444247,
            "unit": "ns",
            "range": "± 465.85744515654596"
          }
        ]
      }
    ]
  }
}