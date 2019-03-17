#!/bin/bash

docker build -t public.api .
docker run -p 127.0.0.1:5001:8080 --name public.api public.api