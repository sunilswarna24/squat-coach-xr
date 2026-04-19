# Protocol

This folder is the **contract** between `pi/` and `quest/`. Any change to the
wire format must be reflected in:

1. `schema.json` — JSON Schema for each message type.
2. `samples/` — concrete example payloads (used by both sides as test fixtures).
3. `../docs/wire-protocol.md` — human-readable spec.

If you only change the schema but not the sample files or the docs, future
contributors (including future-you) will have a bad time. The tests in
`../pi/tests/test_protocol.py` validate each sample against the schema to
catch drift.
