#!/usr/bin/env bash

#############################################
##
## Spectero OpenVPN Authentication Script 
## 
## This script performs various user authentication into the OpenVPN Framework.
##
## It is very crucial that this script handles user data as expected
## While sanitizing against Arbitary Code Execution vulnerabiltiies.
## In the event that vulnerability is found, please report it to the developers by
## Creating an issue in the source code's repository.
##
## End users may find the latest version of this script in the Github Repository, 
## or provided with the latest release of Spectero's daemon software:
## https://github.com/ProjectSpectero/daemon
##
#############################################

# DETERMINE EXECUTION
# This if condition determines if we're a developer
# If the user is a developer, it will execute the relative binary over the installation path.

# Print Current Working Directory
echo $PWD;

if [ ! -f ../../../cli/bin/Debug/netcoreapp2.1/Spectero.daemon.CLI.dll ]; then
    # The daemon is likely running this, run it as expected.
    spectero cli fileauth OpenVPN $1;
else
    # The user is likely a developer, run the relative build.
    dotnet ../../../../../cli/bin/Debug/netcoreapp2.1/Spectero.daemon.CLI.dll fileauth OpenVPN $1;
fi


# Return the exit code.
exit $?;