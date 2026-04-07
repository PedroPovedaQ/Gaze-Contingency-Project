"""
Main analysis entry point.

Usage:
    python run_analysis.py <path_to_GazeData_folder> [output_dir]

Default output: ./results/

Example:
    python run_analysis.py ~/Library/Application\\ Support/DefaultCompany/GazeContingencyProject/GazeData
    python run_analysis.py ./GazeData ./results
"""

import sys
from pathlib import Path

from load_data import load_all_summaries
from plots import generate_all
from stats import save_report


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    data_dir = Path(sys.argv[1]).expanduser()
    out_dir = Path(sys.argv[2]).expanduser() if len(sys.argv) > 2 else Path("results")

    if not data_dir.exists():
        print(f"Error: data directory not found: {data_dir}")
        sys.exit(1)

    print(f"Loading data from: {data_dir}")
    df = load_all_summaries(data_dir)

    if df.empty:
        print("No data loaded. Check that trial_summary.json files exist.")
        sys.exit(1)

    # Save the flattened dataset for spreadsheet/manual review
    out_dir.mkdir(parents=True, exist_ok=True)
    csv_path = out_dir / "all_rounds.csv"
    df.to_csv(csv_path, index=False)
    print(f"  saved {csv_path}")

    # Generate plots
    generate_all(df, out_dir)

    # Run statistical tests
    print("\nRunning statistical tests...")
    save_report(df, out_dir)

    print(f"\nAll outputs in: {out_dir.resolve()}")


if __name__ == "__main__":
    main()
