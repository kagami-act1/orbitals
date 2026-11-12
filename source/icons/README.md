# Google Material Symbols assets

This folder contains the fixed, offline icon snapshot embedded by `build.ps1`.
Normal application builds do not download assets or run Python.

- `catalog.tsv.gz`: UTF-8 rows: name, display label, primary category, search words, rank, vector path. One row per unique codepoint.
- `categories.tsv`: the 18 mutually exclusive primary categories, in display order.
- `classification.tsv`: complete membership, rule provenance, and upstream aliases for review.
- `provenance.json`: upstream commit, style, fixed axes, and counts.
- `LICENSE-Material-Symbols.txt`: upstream Apache 2.0 license. Copyright and modifications are recorded in the application's `THIRD-PARTY-NOTICES.txt`.

The path format is generated from a trusted, pinned upstream font, then embedded
in the executable. Runtime does not load arbitrary SVG/XAML, external fonts, or
downloaded paths. WPF reads it using nonzero winding and frozen geometry.

## Rebuilding the snapshot

Use Python with `fonttools==4.67.0`. Run `build_catalog.py INPUT_DIRECTORY OUTPUT_DIRECTORY`.
The input directory must contain:

- `MaterialSymbolsRounded.ttf`, from the repository commit in `provenance.json`, at `variablefont/MaterialSymbolsRounded[FILL,GRAD,opsz,wght].ttf`.
- `rounded.codepoints`, from the adjacent `.codepoints` file at the same commit.
- `symbols-metadata.json`, from `https://fonts.google.com/metadata/icons?key=material_symbols&incomplete=true`.
- `upstream-commit.txt`, containing the commit SHA.

Metadata is live upstream information. Changes in upstream metadata can affect
labels, aliases, ordering, or categories on a future explicit rebuild; shipped
assets remain fixed until that rebuild is reviewed. Preserve the existing
snapshot rather than silently refreshing it during normal builds.

Category assignment: the primary-object rules in the generator run first;
explicit legacy-name overrides and mapped Google categories cover the remainder.
Unclassified icons cause the generator to fail. Every icon has exactly one
primary category. Search keywords may span purposes without duplicating entries.
