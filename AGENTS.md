# Project entry
Read ../../../AGENTS.md and ../../../Rules/*.md first, then ../SPEC.md, ../HANDOFF.md, ../CHECKLIST.md. This folder is Days/day104/Project.

For mobile Web changes, read docs/MOBILE_PERFORMANCE.md. After rebuilding, run Tools/optimize_web.py on the Web output and Tools/tests/mobile_web.cjs before publishing. Physical mobile fps is a separate check.

Start by reading docs/RETROSPECTIVE_20261028.md. Do not treat desktop/Editor QA or successful deployment as physical-mobile performance verification. Verify a local HTTP server responds before opening its browser URL. Use the configured gh-pages publishing route; avoid adding a duplicate Pages deployment job.
