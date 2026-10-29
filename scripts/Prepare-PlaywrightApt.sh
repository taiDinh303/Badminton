#!/usr/bin/env bash
set -euo pipefail

# GitHub's Ubuntu mirror can stall while fetching Playwright dependencies.
# Preserve suites/components/signature checks; only replace that mirror host.
shopt -s nullglob
sources=(/etc/apt/sources.list /etc/apt/sources.list.d/*.list /etc/apt/sources.list.d/*.sources /etc/apt/apt-mirrors*.txt)
for source in "${sources[@]}"; do
  if [[ -f "$source" ]]; then
    sudo sed -i -E 's|https?://azure\.archive\.ubuntu\.com/ubuntu|https://archive.ubuntu.com/ubuntu|g' "$source"
  fi
done

# These settings also apply to apt invoked by `playwright install --with-deps`.
# Fail clearly on unavailable downloads instead of consuming the job budget.
printf '%s\n' \
  'Acquire::Retries "2";' \
  'Acquire::http::Timeout "20";' \
  'Acquire::https::Timeout "20";' \
  'Acquire::ForceIPv4 "true";' \
  | sudo tee /etc/apt/apt.conf.d/80-playwright-ci-network >/dev/null
