#!/usr/bin/env bash

cd src/BrowserExts/ChromeExt

export DOCKER_RUN="docker run -v $PWD:/app -w /app node:9"

echo Run npm install...
$DOCKER_RUN npm install

echo Build app...
$DOCKER_RUN npm install --global gulp && gulp
