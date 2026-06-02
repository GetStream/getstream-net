#!/usr/bin/env bash

set -euo pipefail

title=""
body=""
body_file=""
output=""
manual_bump=""
use_current_version="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --title)
      title="${2:-}"
      shift 2
      ;;
    --body)
      body="${2:-}"
      shift 2
      ;;
    --body-file)
      body_file="${2:-}"
      shift 2
      ;;
    --output)
      output="${2:-}"
      shift 2
      ;;
    --manual-bump)
      manual_bump="${2:-}"
      shift 2
      ;;
    --use-current-version)
      use_current_version="${2:-}"
      shift 2
      ;;
    *)
      echo "unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -n "${body_file}" ]]; then
  body="$(<"${body_file}")"
fi

determine_bump() {
  local pr_title="$1"
  local pr_body="$2"

  if [[ "${pr_body}" =~ BREAKING[[:space:]-]CHANGE ]] || [[ "${pr_title}" =~ BREAKING[[:space:]-]CHANGE ]]; then
    echo "major"
    return
  fi

  if ! echo "${pr_title}" | grep -Eq '^([a-zA-Z]+)(\([^)]+\))?(!)?:'; then
    echo "none"
    return
  fi

  if echo "${pr_title}" | grep -Eq '^([a-zA-Z]+)(\([^)]+\))?!:'; then
    echo "major"
    return
  fi

  local type
  type="$(echo "${pr_title}" | sed -E 's/^([a-zA-Z]+).*/\1/' | tr '[:upper:]' '[:lower:]')"
  if [[ "${type}" == "feat" ]]; then
    echo "minor"
    return
  fi
  if [[ "${type}" == "fix" || "${type}" == "bug" ]]; then
    echo "patch"
    return
  fi

  echo "none"
}

find_latest_tag() {
  local latest
  latest="$(git tag --list | sed 's/^v//' | grep -E '^[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1)"
  if [[ -z "${latest}" ]]; then
    latest="0.0.0"
  fi
  echo "${latest}"
}

read_csproj_version() {
  local csproj="src/stream-feed-net.csproj"
  local version
  version="$(grep -oE '<Version>[^<]+</Version>' "${csproj}" | head -n 1 | sed -E 's|<Version>([^<]+)</Version>|\1|')"
  if [[ -z "${version}" ]]; then
    echo "could not parse <Version> from ${csproj}" >&2
    exit 1
  fi
  echo "${version}"
}

apply_version() {
  local v="$1"
  # Portable in-place edit: BSD sed (macOS) requires an extension; GNU sed accepts it too.
  sed -i.bak "s|<Version>[^<]*</Version>|<Version>${v}</Version>|" src/stream-feed-net.csproj
  sed -i.bak "s|private const string VersionName = \".*\";|private const string VersionName = \"${v}\";|" src/Client.cs
  rm -f src/stream-feed-net.csproj.bak src/Client.cs.bak
}

write_output() {
  local key="$1"
  local value="$2"
  if [[ -n "${output}" ]]; then
    echo "${key}=${value}" >> "${output}"
  else
    echo "${key}=${value}"
  fi
}

compute_next() {
  local previous="$1"
  local bump="$2"
  local major minor patch
  IFS='.' read -r major minor patch <<< "${previous}"
  case "${bump}" in
    major) echo "$((major + 1)).0.0" ;;
    minor) echo "${major}.$((minor + 1)).0" ;;
    patch) echo "${major}.${minor}.$((patch + 1))" ;;
    *) echo "${previous}" ;;
  esac
}

manual_bump_normalized="$(echo "${manual_bump}" | tr '[:upper:]' '[:lower:]')"
use_current_normalized="$(echo "${use_current_version}" | tr '[:upper:]' '[:lower:]')"

if [[ -n "${manual_bump_normalized}" ]]; then
  case "${manual_bump_normalized}" in
    major|minor|patch) ;;
    *)
      echo "manual-bump must be one of: major, minor, patch" >&2
      exit 1
      ;;
  esac

  previous_version="$(find_latest_tag)"
  if [[ "${use_current_normalized}" == "true" ]]; then
    next_version="$(read_csproj_version)"
  else
    next_version="$(compute_next "${previous_version}" "${manual_bump_normalized}")"
    apply_version "${next_version}"
  fi

  write_output "should_release" "true"
  write_output "bump" "${manual_bump_normalized}"
  write_output "previous_version" "${previous_version}"
  write_output "version" "${next_version}"
  write_output "tag" "v${next_version}"
  exit 0
fi

bump="$(determine_bump "${title}" "${body}")"

if [[ "${bump}" == "none" ]]; then
  write_output "should_release" "false"
  write_output "bump" "none"
  exit 0
fi

previous_version="$(find_latest_tag)"
next_version="$(compute_next "${previous_version}" "${bump}")"
apply_version "${next_version}"

write_output "should_release" "true"
write_output "bump" "${bump}"
write_output "previous_version" "${previous_version}"
write_output "version" "${next_version}"
write_output "tag" "v${next_version}"
