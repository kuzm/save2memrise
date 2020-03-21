# Save to Memrise

This unofficial extension allows you to add a word or phrase to your Memrise in a few clicks. You can add a selection from a web page or type in a word directly. The extension creates a course with saved words that you can then practice on [www.memrise.com](https://www.memrise.com).

To install this extension to your Chrome browser, [go to Chrome Web Store](https://chrome.google.com/webstore/detail/save-to-memrise/jedpoleopoehklpioonelookacalmcfk). 

## Development Setup

### Version Control

Make sure that `~/.ssh/id_rsa.pub` is available on your machine and RSA identity is added to the SSH agent: 
```
eval `ssh-agent -s`
ssh-add -K ~/.ssh/id_rsa
```

If ssh-add cannot connect to the agent, check that SSH_AUTH_SOCK and
SSH_AGENT_PID are set. 

It is convenient to browse the Git repo with SourceTree or Sublime Merge. If your SourceTree cannot connect to the repository, open it from the terminal with the configured SSH agent: 
```
open /Applications/Sourcetree.app
```
or 
```
open /Applications/Sublime\ Merge.app
```

### Docker

Build and run locally with `docker-compose`:
```
docker-compose down
docker-compose up --force-recreate --build
```

### AWS CLI in Docker

The `mesosphere/aws-cli` image provides containerized AWS CLI on alpine to avoid requiring the AWS CLI to be installed. Read more on [Docker Hub](https://hub.docker.com/r/mesosphere/aws-cli). 

Usage:
1. Make sure, that `.aws` folder exists:
```
mkdir ~/.aws
```

2. Run docker in interactive mode: 
```
docker run -v "$(pwd):/project" -v ~/.aws:/root/.aws -it --entrypoint /bin/sh mesosphere/aws-cli
```

3. (Optional) If your `.aws` folder is empty, execute:
```
aws configure
```


## Chrome extension

### Development Setup

Make sure that your chrome extension is configured for localhost:
- Adjust settings to localhost in `src/BrowserExts/ChromeExt/config.js`.
- Allow localhost in `src/BrowserExts/ChromeExt/manifest.json` by adding `"*://localhost/"` in `permissions` section. 

When starting from scratch, follow instructions below:

1. Install Node.js
2. Install Gulp
3. `npm install`
4. Install Semantic-UI
```
npm install semantic-ui --save
cd semantic/
gulp build
```
5. `gulp` or `gulp browserify`

For the regular build, run:
```
./build.sh --target=BuildChromeExt
```

### Install unpacked extension

To load an unpacked extension to the browser, follow the following steps: 
1. Visit chrome://extensions in your browser. 
2. Ensure that the Developer mode checkbox in the top right-hand corner is checked.
3. Click Load unpacked extension… to pop up a file-selection dialog. 
4. Navigate to the directory in which your extension files live (`src/BrowserExts/ChromeExt/build`), and select it.

If the extension is valid, it'll be loaded up and active right away. If it's invalid, an error message will be displayed at the top of the page. Correct the error, and try again.


## Services

### Development Setup

1. Install Docker
2. Install Visual Studio Code with OmniSharp plugin

### Run tests

Run tests with dotnet:
```
cd test/Services/Public.API.UnitTests/
dotnet run --project Public.API.UnitTests.csproj
```

```
cd est/Services/Public.API.IntegrationTests
dotnet run --project Public.API.IntegrationTests.csproj
```

Run tests with cake:
```
/build.sh --target=UnitTest
/build.sh --target=IntegrationTest
```


## AWS Prerequisites

1. Create ACM Certificate in us-east-1 region by creating CloudFormation stack from the following file: `Save2MemriseCertificateStack.CFTemplate.yml`. 

> [How do I migrate my SSL certificate to the US East (N. Virginia) region?](https://aws.amazon.com/premiumsupport/knowledge-center/migrate-ssl-cert-us-east/) You can't migrate an existing certificate in ACM from one AWS Region to another. To associate an ACM Certificate with a CloudFront distribution, you must create a certificate in the US East (N. Virginia) Region.


## Deployment

Deployment is performed by AWS CodePipeline. CodePipeline has the following steps configured: 
1. GitHub as a source repository. 
2. CodeBuild runs unit and integration tests.
    * CodeBuild reads `test-public-api-buildspec.yml` for build instructions.  
3. CodeBuild builds docker images and pushes them to ECR. 
    * CodeBuild reads `build-public-api-image-buildspec.yml` for build instructions.  
3. CodeDeploy pulls docker images from ECR and deploys them to ECS. 
4. CodeBuild builds chrome extension, uploads to S3, and invalidates CloudFront cache. 
    * CodeBuild reads `chrome-ext-buildspec.yml` for build instructions.

AWS CodePipeline is configured to build solution and run tests inside a docker container which is based on `build.Dockerfile`. The corresponding docker image is pushed to ECR. You can use this docker image locally to build and run tests as if it was done by CodePipeline: 
```
docker run -v "$(pwd):/src" -v ~/.aws:/root/.aws -it --entrypoint /bin/sh 321373361512.dkr.ecr.eu-central-1.amazonaws.com/save2memrise/build:0.1.0
```

Two environments are defined: blue and green. Both are production environments. 
Green environment is a main one for stable releases. The blue environment is intended for unstable releases. Once a release is tested, live traffic should be switched from the green env to the blue env. If no issues are found, the release can be promoted to the green env and live traffic can be switched to the green env. If some issues are detected, the release should not be promoted, and live traffic should be switched back to the green env which still runs the stable version. When inactive, the blue env can be deactivated to optimize costs. 

Logs are collected by AWS CloudWatch in the log group `save2memrise`. 

Public.Api Service exposes two system endpoints: 
* `/_system/health`
* `/_system/ping`

### Domain name and e-mail service

Domain name `save2memrise` is provided by Google Domains. 
Domain name is connected to AWS Route 53 as described here: [How To: Connecting Google Domains to Amazon S3](https://medium.com/@limichelle21/connecting-google-domains-to-amazon-s3-d0d9da467650)
Route 53 defines two main subdomains:
* `api2.save2memrise.com`
* `chromeext2.save2memrise.com`

E-mail service is available at *@save2memrise.com. 

To enable email forwarding, add MX record to Route 53 as described here: [How do I forward emails back to gmail while using AWS custom name servers to host my website?](https://www.reddit.com/r/aws/comments/5o5yjh/how_do_i_forward_emails_back_to_gmail_while_using/).


### Update CloudFormation stack in AWS

Warning! It is strongly recommended to apply updates on the root stack, even if only a nested stack has been changed. 

1. AWS CLI is available as Docker image. Read the section *AWS CLI in Docker* above for the instructions. 

2. Navigate to the folder with CloudFormation templates:
```
cd infra/CloudFormation
```

3. Upload the current version of CloudFormation templates to S3:
```
./upload-cftemplates-to-s3.sh
```

4. Create a change set:
```
aws cloudformation create-change-set --stack-name Save2MemriseStack2 --template-url https://s3.amazonaws.com/save2memrisestack2-cloudformationbucket/Save2MemriseStack2.CFTemplate.yml  --change-set-name=Save2MemriseStack2ChangeSet
```

5. List the created change sets:
```
aws cloudformation list-change-sets --stack-name Save2MemriseStack2
```

6. View the change set:
```
aws cloudformation describe-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

Alternatively, you can see change set details in AWS Console. 

7. Execute the change set: 
```
aws cloudformation execute-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

8. View the progress of change set execution:
```
aws cloudformation describe-stacks --stack-name Save2MemriseStack2
```

Status of the stack should be `UPDATE_COMPLETE`. If not, go to CloudFormation in AWS Console and view log messages for this stack. 

9. Delete a change set:
```
aws cloudformation delete-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

NB: If you get the encoded log message like this "API: ec2:RevokeSecurityGroupEgress You are not authorized to perform this operation. Encoded authorization failure message: ...", run this command to decode:
```
aws sts decode-authorization-message --encoded-message <value>
```

### Publish to Chrome Web Store

1. Increment the version of the extension in `version.txt`
2. Execute `./build.sh --target=BuildChromeExt` 
3. Compress the content of `src/BrowserExts/ChromeExt/build` directory as ZIP archive
4. Go to [Chrome Web Store Console](https://chrome.google.com/webstore/developer/dashboard?authuser=1) and sign in as `save2memrise@gmail.com` user
5. Upload the archive

The extension is published to [Chrome Web Store](https://chrome.google.com/webstore/detail/save-to-memrise/jedpoleopoehklpioonelookacalmcfk). 

To publish the extension to the world, you need to re-publish with other visibility options.

### Switch between Green and Blue production environments

To make Green or Blue active environment, update CloudFormation stack:
1. Go to `Save2MemriseStack2.CFTemplate.yml` and set `ActiveProdEnvironment` parameter to `blue` or `green`. Alternatively, you can specify `ActiveProdEnvironment` parameter as command line argument. 
2. Follow instructions in _Update CloudFormation stack in AWS_ section above to apply changes to AWS. 
3. To verify in AWS Console, that traffic was switched successfully: 
3.1. Go to EC2 > Load Balancers > Listeners > Rules. Default routing should forward traffic to the selected environment. 
3.2. Go to Route 53 > Hosted Zones and CloudFront > Distributions and check that `chromeext2.save2memrise.com` is mapped to the proper distribution. 


## Investigate Memrise API 

Sniff traffic from www.memrise.com by using Developer tools in Chrome. Memrise REST API is no longer used. 

## Troubleshooting

### Status of AWS::CertificateManager::Certificate is stuck with CREATE_IN_PROGRESS

Check your email and approve a certificate creation for each subdomain. 
