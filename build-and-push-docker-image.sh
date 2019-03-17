#!/bin/bash
REPO_NAME=$1
if [ $# -ne 1 ]; then
    echo $0: usage: $0 REPO_NAME
    exit 1
fi

$(aws ecr get-login --no-include-email --region eu-central-1)
docker build -t $REPO_NAME .
docker tag $REPO_NAME:latest 321373361512.dkr.ecr.eu-central-1.amazonaws.com/$REPO_NAME:latest
docker push 321373361512.dkr.ecr.eu-central-1.amazonaws.com/$REPO_NAME:latest
