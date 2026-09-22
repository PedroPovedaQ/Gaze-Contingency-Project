#!/usr/bin/env python3
"""Build a standalone, pdfLaTeX-compatible edition of the annotated run sheet."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/protocol/experimenter-run-sheet.md'
OUTPUT = ROOT / 'docs/protocol/overleaf/experimenter-run-sheet.tex'


def tex(value: str) -> str:
    value = re.sub(r'\*\*(.*?)\*\*', r'\1', value)
    value = value.replace('\\"', '"').replace('¨', '')
    value = value.translate(str.maketrans({'“': '``', '”': "''", '’': "'", '‘': '`',
                                         '—': '---', '–': '--', '→': r'$\rightarrow$'}))
    escapes = {'&': r'\&', '%': r'\%', '#': r'\#', '_': r'\_',
               '{': r'\{', '}': r'\}', '$': r'\$', '~': r'\textasciitilde{}',
               '^': r'\textasciicircum{}', '\\': r'\textbackslash{}'}
    return ''.join(escapes.get(c, c) for c in value)


PREAMBLE = r'''% Standalone Overleaf document. Compile this file with pdfLaTeX.
% Generated from the annotated Markdown; regenerate with scripts/format-overleaf-run-sheet.py.
\documentclass[11pt,letterpaper]{article}
\usepackage[T1]{fontenc}
\usepackage[utf8]{inputenc}
\usepackage{lmodern,microtype}
\usepackage[margin=0.75in,headheight=16pt]{geometry}
\usepackage{xcolor}
\definecolor{Gold}{HTML}{FFC904}
\definecolor{Ink}{HTML}{171717}
\definecolor{Gray}{HTML}{626262}
\definecolor{ActionInk}{HTML}{665117}
\definecolor{NoteFill}{HTML}{FFF8DD}
\usepackage{titlesec,enumitem,tabularx,fancyhdr,needspace}
\usepackage[most]{tcolorbox}
\usepackage{tikz}
\usetikzlibrary{arrows.meta}
\usepackage{xurl}
\usepackage[colorlinks=true,linkcolor=Ink,urlcolor=ActionInk]{hyperref}
\hypersetup{pdftitle={Gaze Contingency Search Study - Annotated Experimenter Run Sheet},pdfauthor={Gaze Contingency Project}}
\renewcommand{\familydefault}{\sfdefault}
\color{Ink}
\setlength{\parindent}{0pt}
\setlength{\parskip}{6pt}
\linespread{1.06}
\setlist[itemize]{leftmargin=1.25em,itemsep=4pt,topsep=3pt}
\setcounter{secnumdepth}{0}
\titleformat{\section}{\Large\bfseries}{}{0pt}{}[{\color{Gold}\titlerule[1.4pt]}]
\titlespacing*{\section}{0pt}{16pt}{10pt}
\pagestyle{fancy}
\fancyhf{}
\fancyhead[L]{\small\color{Gray}FOLLOW MY VOICE \enspace / \enspace RUN SHEET}
\fancyhead[R]{\small\color{Gray}ANNOTATED REVIEW}
\fancyfoot[L]{\footnotesize\color{Gray}Source v0.1 \textbullet{} Formatted 22 September 2026}
\fancyfoot[R]{\small\thepage}
\renewcommand{\headrulewidth}{0.35pt}
\emergencystretch=2em
\widowpenalty=10000
\clubpenalty=10000
\newcommand{\step}[1]{\Needspace{7\baselineskip}\section{#1}}
\newcommand{\say}[2]{\Needspace{4\baselineskip}\par\textbf{#1}\enspace #2\par}
\newcommand{\action}[2]{\Needspace{3\baselineskip}\par{\color{ActionInk}\textbf{#1}\enspace\textit{#2}}\par}
\newtcolorbox{meetingnote}[1]{enhanced,breakable,colback=NoteFill,colframe=Gold!70!black,
 boxrule=0.5pt,arc=1mm,left=9pt,right=9pt,top=7pt,bottom=7pt,
 title={#1},colbacktitle=Gold,coltitle=Ink,fonttitle=\small\bfseries}
\newcommand{\meeting}[2]{\Needspace{7\baselineskip}\begin{meetingnote}{#1}#2\end{meetingnote}}
\newcommand{\field}[1]{\textbf{#1}\enspace\hrulefill}
\begin{document}
\hypersetup{pageanchor=false}
\begin{titlepage}
\thispagestyle{empty}
\vspace*{0.3in}
{\small\bfseries\color{Gray}GAZE CONTINGENCY SEARCH STUDY\par}
{\color{Gold}\rule{\linewidth}{4pt}}
\vspace{0.2in}
\begin{tcolorbox}[colback=Ink,colframe=Ink,arc=0mm,left=20pt,right=20pt,top=25pt,bottom=25pt]
{\color{Gold}\bfseries EXPERIMENTER RUN SHEET\par}
\vspace{12pt}
{\color{white}\fontsize{32}{37}\selectfont\bfseries Follow My Voice\par}
\vspace{12pt}
{\color{white}\large Annotated meeting edition\par}
\end{tcolorbox}
\vspace{0.18in}
\begin{tabularx}{\linewidth}{@{}lX@{}}
\textbf{Source} & Version 0.1, September 16, 2026; meeting annotations retained \\[7pt]
\textbf{Formatted} & September 22, 2026 \\[7pt]
\textbf{Setting} & Seated, rotating mixed-reality object search \\[7pt]
\textbf{Hardware} & HTC Vive Focus Vision \\[7pt]
\textbf{Status} & Protocol proposal / researcher review \\
\end{tabularx}
\vspace{12pt}
\begin{meetingnote}{READ BEFORE REHEARSAL}
The supplied run sheet is preserved below, including its questions and meeting notes.
The newer meeting requirements specify \textbf{participant-preferred versus self-similar voice}
and \textbf{controller ray + trigger selection}, with independently measured gaze.
Older generic-voice and gaze-dwell wording in this source remains to be reconciled.

The provisional plan is 30 experimental trials per voice plus two practice trials.
Audited \texttt{v2} still implements 14 measured gaze-dwell trials with neutral profiles.
This edition does not freeze method decisions or certify participant readiness.
\end{meetingnote}
\vfill
{\small\color{Gray}UCF-inspired black and gold. Editable, standalone LaTeX.\par}
\href{https://github.com/PedroPovedaQ/Gaze-Contingency-Project/blob/v2/docs/meeting-reconciliation-2026-09-22.md}{\small Current decisions, progress and implementation backlog}
\end{titlepage}
\hypersetup{pageanchor=true}
\step{How to use this edition}
\say{SAY / ASK}{Participant-facing speech is set in dark upright type.}
\action{ACTION / RESEARCHER}{Experimenter-only instructions are italic and muted gold-brown. Do not read them aloud.}
\meeting{MEETING NOTE / OPEN DECISION}{Gold panels preserve questions, proposed updates and unresolved points from the supplied source. They are not participant instructions or validated implementation claims. Source numbering is retained.}
\begin{tabularx}{\linewidth}{@{}XX@{}}
\field{Participant ID} & \field{Run} \\[14pt]
\field{Date} & \field{Researcher} \\[14pt]
\field{Build} & \field{Voice order} \\[14pt]
\field{Neutral profile (source)} & \field{Survey version} \\
\end{tabularx}
\vspace{8pt}
'''

DIAGRAM = r'''
\begin{center}
\begin{tikzpicture}[scale=0.8]
\foreach \a in {0,45,...,315}{
  \begin{scope}[shift={(\a:2.6)},rotate=\a-90]
  \draw[rounded corners=1pt,draw=Gray,fill=white] (-0.55,-0.16) rectangle (0.55,0.16);
  \foreach \x in {-0.36,-0.24,-0.12,0,0.12,0.24,0.36}{\fill[Gold!80!black] (\x,0) circle (0.025);}
  \end{scope}
}
\draw[Ink,line width=1pt,rounded corners=3pt] (-0.42,-0.48) rectangle (0.42,0.37);
\fill[Ink] (0,0.18) circle (0.21);
\draw[Ink,line width=3pt] (-0.25,-0.13)--(0.25,-0.13);
\draw[Ink,line width=2pt] (-0.14,-0.16)--(-0.14,-0.42) (0.14,-0.16)--(0.14,-0.42);
\draw[-{Stealth[length=3mm]},line width=1.3pt,Gold!70!black] (35:1.35) arc (35:325:1.35);
\node[font=\small,align=center] at (0,-3.35) {Remain seated. Turn to look around.};
\node[font=\small\bfseries] at (0,3.12) {Objects can appear around you};
\end{tikzpicture}

{\small\color{Gray}Schematic, not to scale. Illustrates seated rotation only.}
\end{center}
'''

text = SOURCE.read_text()
researcher = re.search(r'^Researcher only: (.+)$', text, re.M).group(1)
parts = [PREAMBLE, '\\action{RESEARCHER ONLY}{'+tex(researcher)+'}\n']
sections = re.split(r'^## ', text, flags=re.M)[1:]
break_before = {'UPDATES', '5. Familiarization', '6 First task block', '9 Final Qualtrics',
                'Final researcher check'}
for section in sections:
    heading, _, content = section.partition('\n')
    heading = heading.strip()
    if heading in break_before:
        parts.append('\\clearpage\n')
    if heading == 'UPDATES':
        parts.append('\\step{Meeting updates}\\meeting{RESEARCHER NOTES}{The following requirements were added to the supplied run sheet. They remain proposals/open decisions; the GitHub backlog owns implementation and validation.}\n')
    elif heading.startswith('QUESTION'):
        pass
    elif 'USE IMAGE HERE' in heading:
        parts.append('\\meeting{MEETING NOTE}{The source requests a simple seated 360-degree illustration. The schematic below adds that visual without specifying a selection method.}\n' + DIAGRAM)
    elif heading == 'NOTE':
        pass
    else:
        parts.append('\\step{'+tex(heading)+'}\n')
    blocks = re.split(r'\n\s*\n', content.strip())
    for block in blocks:
        block = block.strip()
        if not block:
            continue
        if block.startswith('- '):
            items = re.split(r'\n- ',block[2:])
            # Keep all source notes and citations. In Updates, these are research notes.
            body = '\\begin{itemize}\n' + '\n'.join('\\item {'+tex(x.replace('\n',' '))+'}' for x in items) + '\n\\end{itemize}\n'
            if heading == 'UPDATES':
                parts.append('\\begin{meetingnote}{MEETING UPDATES / PROPOSED MEASURES}\n'+body+'\\end{meetingnote}\n')
            else:
                parts.append('{\\color{ActionInk}\\itshape\n'+body+'}\n')
            continue
        block = block.replace('\n', ' ')
        speech = re.match(r'^(Say|Ask|At completion say|Before continuing ask):\s*(.*)',block)
        if speech:
            parts.append('\\say{'+speech[1].upper()+'}{'+tex(speech[2])+'}\n')
        elif block.startswith('ACTION:'):
            parts.append('\\action{ACTION}{'+tex(block[7:].strip())+'}\n')
        elif block.startswith('PAUSE OR STOP:'):
            parts.append('\\meeting{PAUSE OR STOP}{'+tex(block[14:].strip())+'}\n')
        elif block.startswith('Exploratory follow-up:'):
            parts.append('\\action{EXPLORATORY FOLLOW-UP}{'+tex(block.split(':',1)[1].strip())+'}\n')
        elif block.startswith('Researcher settings:'):
            parts.append('\\action{RESEARCHER SETTINGS}{'+tex(block.split(':',1)[1].strip())+'}\n')
        elif heading.startswith('QUESTION'):
            parts.append('\\meeting{OPEN QUESTION FROM MEETING}{'+tex(block)+'}\n')
        elif block.startswith('Callibration') or (heading == '3 Fit and calibrate the headset' and 'offset' in block):
            parts.append('\\meeting{CALIBRATION NOTE / PROPOSAL}{'+tex(block)+'}\n')
        elif 'INTERNAL SETTINGS' in block or block.startswith('READ ONLY'):
            parts.append('\\action{INTERNAL SETTINGS}{'+tex(block.replace('**INTERNAL SETTINGS**:', '').replace('INTERNAL SETTINGS', '').strip())+'}\n')
        elif heading.startswith('Appendix'):
            parts.append('\\meeting{CURRENT RECORDING PASSAGE / SOURCE TEXT}{'+tex(block)+'}\n')
        else:
            parts.append(tex(block)+'\n\n')
parts.append('\\end{document}\n')
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
OUTPUT.write_text(''.join(parts))
print(OUTPUT)
