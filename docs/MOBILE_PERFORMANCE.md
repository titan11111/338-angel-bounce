# Mobile Web performance — 2026-10-28 JST

## Incident and evidence
Phone owner reports the published game is too slow to play. No device model, fps or browser profile was available, so a single exclusive root cause has NOT been proven.

Confirmed contributors from shipped files:
- Web.data = 33,942,702 bytes; Web.wasm = 33,792,090 bytes. Together 67,734,792 bytes before framework/assets.
- The fullscreen Unity loader did not override devicePixelRatio. A DPR=3 phone requests nine times the render pixels of DPR=1 at the same CSS size.
- Existing QA was desktop/Editor only; physical mobile performance was NOT_TESTED.

## Emergency correction and publication
- gh-pages commit 6361fc333b3aed8159cb0a5740fe449777d89f25 caps mobile DPR at 1 and the longer render dimension at 960 pixels. Tablet/coarse-pointer detection includes iPad desktop user agents. Resize updates the cap.
- Desktop DPR is capped at 1.5. Game rules, physics, art, audio and input handling are retained.
- Tools/optimize_web.py reapplies this patch after a Unity rebuild. It also creates gzip variants and uses native DecompressionStream with original-file fallback, without requiring custom Content-Encoding headers on GitHub Pages.
- Verified local gzip output: data 27,620,557 bytes; wasm 8,810,180 bytes; combined 36,430,737 bytes, a 46.2% reduction. This is transfer-size reduction, NOT an fps measurement or a reduction of decompressed Unity memory.
- .github/workflows/mobile-web.yml runs optimization and publishes the resulting gh-pages build on changes to optimizer/workflow. Compressed output and HTML were read back from gh-pages commit ecaf0f8b623fe63a73ffdd83686150384f58ede0. Pages build/deployment run 40692039297 completed successfully. Updated gameplay and public HTTP hash equality remain unverified. The optimizer workflow uses the existing gh-pages branch publication; duplicate Actions deployment jobs were removed after environment rejection.

## Repeatable verification
1. After Titan > Build Web, run `python3 Tools/optimize_web.py ../Builds/Web` from Project before publishing.
2. Run `node Tools/tests/mobile_web.cjs ../Builds/Web/index.html`.
3. Gzip round-trip checks must preserve the original data/wasm SHA-256. The script performs round-trip checks itself.
4. Read back the published HTML and compressed artifacts, verify hashes, then play on iPhone Safari and Android Chrome.
5. Measure cold-start time and steady gameplay fps on an identified physical device; target 30fps minimum. Test move + rotate with two fingers, launch, retry, sound and background/return.

## Next-project lesson
Treat download latency, decompression/startup and steady rendering as separate budgets. Start mobile Web at DPR<=1 with a bounded render dimension, inspect transferred bytes, and measure a physical phone before calling it mobile-ready. A 100MiB single-file GitHub limit is not a usable mobile performance budget. Compression does not fix GPU fill cost or decoded texture/audio memory. Preserve original assets and use evidence before lowering their quality.

## Current validation limits
Inline JavaScript syntax, portrait/landscape phone caps, tablet cap, desktop cap, missing-decompressor fallback and gzip byte identity passed locally. Local HTTP binding is denied by the host sandbox. In-app browser has no usable route; native browser control returned noWindowsAvailable. Therefore updated browser gameplay, console errors, public HTTP hash equality and physical iPhone/Android fps remain NOT_TESTED. GitHub source/index writes are verified through the connector.
