# XR Visual Search and Proximity-Hint Paper Archive

This folder is the local paper archive for the Gaze Contingency literature inventory. It prioritizes visual-search, attention-guidance, and proximity-feedback work from IEEE ISMAR, IEEE VR, and ACM CHI, with a small number of directly relevant papers from other venues.

## Archive policy

- `manifest.csv` is the source of truth for inclusion, venue track, DOI, relevance, and local availability.
- A PDF is stored only when a lawful open-access copy is available from an author, institutional repository, proceedings archive, or publisher.
- A missing local PDF does not mean the paper was excluded. Use its DOI or source URL in `manifest.csv`.
- Stored filenames begin with publication year and first author for predictable sorting.
- Conference papers, adjunct papers, workshop papers, posters, extended abstracts, and TVCG journal-track papers are labeled separately.
- Selection-only papers are retained because their feedback mappings may transfer to search assistance, but they must not be described as visual-search experiments.

## Relevance labels

- `direct-search`: participants locate targets among distractors or within an environment.
- `guidance`: evaluates cues that direct attention or indicate out-of-view targets.
- `proximity-retrieval`: proximity relationships help users retrieve a previously inspected object.
- `proximity-selection`: distance-dependent feedback supports final target acquisition rather than search.
- `agent-reference`: informs gaze- or speech-based interaction with an assistant.
- `review`: synthesizes a literature family rather than reporting a new search experiment.

## Important interpretation

The word *proximity* is used in different ways across this archive. NeighboAR uses physical proximity between scene objects to select a contextual landmark. Hu et al. and Ariza et al. vary feedback according to cursor-to-target distance during selection. Warden et al. use *display proximity* to describe placing information perceptually close to its referent. These constructs are related but are not interchangeable.
