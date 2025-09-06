#!/usr/bin/env bash
set -e
mvn -f .. -pl testapp -am package -DskipTests
mvn exec:java "$@"

