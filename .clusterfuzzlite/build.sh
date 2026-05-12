#!/usr/bin/env bash
set -euo pipefail

# Publish the fuzz harness as a self-contained executable into $OUT.
dotnet publish fuzz/Granit.QueryEngine.Fuzz/Granit.QueryEngine.Fuzz.csproj \
    -c Release \
    -o "$OUT/queryengine_fuzz"

# Bundle the seed corpus where ClusterFuzzLite expects: <fuzzer>_seed_corpus.zip
( cd fuzz/Granit.QueryEngine.Fuzz/seeds && zip -r "$OUT/queryengine_fuzz_seed_corpus.zip" . )
