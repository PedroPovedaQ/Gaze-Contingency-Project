#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python_bin="${IRB_PYTHON:-python3}"
submission_dir="${repo_dir}/docs/irb/submission"
stage_dir="$(mktemp -d "${repo_dir}/docs/irb/.submission-stage.XXXXXX")"
backup_dir=""
committed=false

cleanup() {
  if [ -d "${stage_dir}" ]; then
    rm -rf -- "${stage_dir}"
  fi
  if [ -n "${backup_dir}" ] && [ -d "${backup_dir}" ]; then
    if [ "${committed}" = false ]; then
      rm -rf -- "${submission_dir}"
      mv -- "${backup_dir}" "${submission_dir}"
    else
      rm -rf -- "${backup_dir}"
    fi
  elif [ "${committed}" = false ] && [ -d "${submission_dir}" ] && [ ! -d "${stage_dir}" ]; then
    rm -rf -- "${submission_dir}"
  fi
}
trap cleanup EXIT

if ! "${python_bin}" -c 'import docx' >/dev/null 2>&1; then
  echo "IRB packet build requires Python 3 with python-docx." >&2
  echo "Install python-docx or set IRB_PYTHON to a compatible Python executable." >&2
  exit 1
fi

"${python_bin}" "${repo_dir}/scripts/build-irb-packet.py" --output-dir "${stage_dir}"
"${python_bin}" "${repo_dir}/scripts/verify-irb-packet.py" --output-dir "${stage_dir}"

if [ -e "${submission_dir}" ]; then
  backup_dir="$(mktemp -d "${repo_dir}/docs/irb/.submission-backup.XXXXXX")"
  rmdir "${backup_dir}"
  mv -- "${submission_dir}" "${backup_dir}"
fi

if ! mv -- "${stage_dir}" "${submission_dir}"; then
  if [ -n "${backup_dir}" ] && [ -d "${backup_dir}" ]; then
    mv -- "${backup_dir}" "${submission_dir}"
    backup_dir=""
  fi
  exit 1
fi

if ! "${python_bin}" "${repo_dir}/scripts/verify-irb-packet.py" --output-dir "${submission_dir}"; then
  rm -rf -- "${submission_dir}"
  if [ -n "${backup_dir}" ] && [ -d "${backup_dir}" ]; then
    mv -- "${backup_dir}" "${submission_dir}"
    backup_dir=""
  fi
  exit 1
fi

committed=true
stage_dir=""
if [ -n "${backup_dir}" ]; then
  rm -rf -- "${backup_dir}"
  backup_dir=""
fi
