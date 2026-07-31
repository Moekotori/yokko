# Yokko MinaCalc native adapter

This native library embeds Etterna's unmodified MinaCalc v515 implementation
and exposes the small C ABI consumed by `Yokko.Core`.

Upstream source:

- Repository: <https://github.com/etternagame/etterna>
- Commit: `b65660062ef2a23121e331c36e23c23a8f6eafaa`
- Original path: `src/Etterna/MinaCalc`
- Note row ABI: `src/Etterna/Models/NoteData/NoteDataStructures.h`
- License: MIT; the original text is retained at
  `vendor/etterna/LICENSE`.

Only `minacalc_adapter.cpp` and this CMake project are Yokko-specific. The
adapter removes the middle column from odd-key charts because the pinned
MinaCalc version intentionally excludes that column from its hand masks. One
standalone-build guard was added around Etterna's runtime XML parameter loader
in `MinaCalc.cpp`; it does not alter the compiled default parameters or MSD
calculation. The vendored calculator files should otherwise be refreshed from
one pinned upstream commit as a unit.
