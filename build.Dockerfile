FROM cakebuild/cake:v0.32.1-2.1-sdk AS builder

# Install Node.JS and Gulp
RUN apt-get update -qq \
    && curl -sL https://deb.nodesource.com/setup_9.x | bash - \
    && apt-get install -y nodejs=9.11.2-1nodesource1 \
    && npm install --global gulp

# Install AWS CLI
RUN apt-get update -qq \
    && apt-get install -y python=2.7.13-2 python-pip=9.0.1-2 \
    && pip install awscli==1.16.140
    
# Install Docker
RUN apt-get update -qq \
    && apt-get install -y apt-transport-https dirmngr \
    && echo 'deb https://apt.dockerproject.org/repo debian-stretch main' >> /etc/apt/sources.list \
    && apt-key adv --keyserver hkp://p80.pool.sks-keyservers.net:80 --recv-keys F76221572C52609D \
    && apt-get update -qq \
    && apt-get install -y docker-engine=17.05.0~ce-0~debian-stretch
    
VOLUME /root/.aws
