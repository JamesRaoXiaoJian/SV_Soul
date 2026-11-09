#!/usr/bin/env bash
# Optional persistent SDK environment. Override SV_SOUL_ENV_ROOT for another host.
sv_soul_env_root="${SV_SOUL_ENV_ROOT:-$HOME/RXJ/environments}"
if [[ -x "$sv_soul_env_root/dotnet/dotnet" ]]; then
    export DOTNET_ROOT="$sv_soul_env_root/dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$sv_soul_env_root/dotnet-cli}"
    export NUGET_PACKAGES="${NUGET_PACKAGES:-$sv_soul_env_root/nuget/packages}"
    mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"
elif ! command -v dotnet >/dev/null 2>&1; then
    echo 'Install the .NET SDK or set SV_SOUL_ENV_ROOT to its persistent environment directory.' >&2
    return 1 2>/dev/null || exit 1
fi
unset sv_soul_env_root
