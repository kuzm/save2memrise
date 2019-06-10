#!/usr/bin/env sh

echo Updating... &&

  # Version 2
  aws s3 cp Save2MemriseCodePipelineStack2.CFTemplate.yml s3://save2memrisestack2-cloudformationbucket/ &&
  aws s3 cp Save2MemriseStack2.CFTemplate.yml s3://save2memrisestack2-cloudformationbucket/ &&
  aws s3 cp Save2MemriseDeployableStack2.CFTemplate.yml s3://save2memrisestack2-cloudformationbucket/ &&
  aws s3 cp Save2MemriseECSClusterStack2.CFTemplate.yml s3://save2memrisestack2-cloudformationbucket/ &&
  
  echo Success! 