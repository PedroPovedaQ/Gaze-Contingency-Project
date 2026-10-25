#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
latex_cmd="${LUALATEX:-lualatex}"
bibtex_cmd="${BIBTEX:-bibtex}"
"$latex_cmd" -interaction=nonstopmode -halt-on-error main.tex
"$bibtex_cmd" main
"$latex_cmd" -interaction=nonstopmode -halt-on-error main.tex
"$latex_cmd" -interaction=nonstopmode -halt-on-error main.tex
cp main.pdf follow-my-voice-thesis-first-pass.pdf
