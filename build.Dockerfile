FROM cakebuild/cake:v0.32.1-2.1-sdk AS builder

RUN apt-get update -qq \
    && curl -sL https://deb.nodesource.com/setup_9.x | bash - \
    && apt-get install -y nodejs

RUN npm install --global gulp
