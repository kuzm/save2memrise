# Save to Memrise

This unofficial extension allows you to add a word or phrase to your Memrise in a few clicks. You can add a selection from a web page or type in a word directly. The extension creates a course with saved words that you can then practice on https://decks.memrise.com.

To install this extension to your Chrome browser, [go to Chrome Web Store](https://chrome.google.com/webstore/detail/save-to-memrise/jedpoleopoehklpioonelookacalmcfk). 

## Development Setup

### Version Control

The source code of the project is hosted on AWS CodeCommit. To connect to the git repo in CodeCommit via SSH, add the following to `~/.ssh/config`:
```
Host git-codecommit.*.amazonaws.com
  User <your-user>
  IdentityFile ~/.ssh/id_rsa
```

Read [Setup Steps for SSH Connections to AWS CodeCommit Repositories on Linux, macOS, or Unix](https://docs.aws.amazon.com/codecommit/latest/userguide/setting-up-ssh-unixes.html)

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

### AWS CLI in Docker

The mesosphere/aws-cli image provides containerized AWS CLI on alpine to avoid requiring the AWS CLI to be installed. Read more https://hub.docker.com/r/mesosphere/aws-cli. 

Usage:
1. Make sure, that `.aws` folder exists:
```
mkdir ~/.aws
```

2. Run docker in interactive mode: 
docker run -v "$(pwd):/project" -v "/Users/mkuz/.aws:/root/.aws" -it --entrypoint /bin/sh mesosphere/aws-cli

Note, change `/Users/mkuz/.aws` to a proper path.

3. (Optional) If your `.aws` folder is empty, execute:
```
aws configure
```


### Docker

Build and run locally with `docker-compose`:
```
docker-compose down
docker-compose up --force-recreate --build
```


## Chrome extension

To load an unpacked extension to the browser, follow the following steps: 
1. Visit chrome://extensions in your browser. 
2. Ensure that the Developer mode checkbox in the top right-hand corner is checked.
3. Click Load unpacked extension… to pop up a file-selection dialog. 
4. Navigate to the directory in which your extension files live, and select it.

If the extension is valid, it'll be loaded up and active right away. If it's invalid, an error message will be displayed at the top of the page. Correct the error, and try again.

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
1. AWS CodeCommit as a source repository. 
2. AWS CodeBuild to build docker images and push them to ECR. 
2.1. CodeBuild reads `buildspec.yml` for build instructions.  
3. AWS CodeDeploy which pulls docker images from ECR and deploys them to ECS. 
4. AWS CodeBuild with builds chrome extension and uploads to S3. 

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

2. Upload the current version of CloudFormation templates to S3:
```
./upload-cftemplates-to-s3.sh
```

3. Create a change set:
```
aws cloudformation create-change-set --stack-name Save2MemriseStack2 --template-url https://s3.amazonaws.com/save2memrisestack2-cloudformationbucket/Save2MemriseStack2.CFTemplate.yml  --change-set-name=Save2MemriseStack2ChangeSet
```

4. List the created change sets:
```
aws cloudformation list-change-sets --stack-name Save2MemriseStack2
```

5. View the change set:
```
aws cloudformation describe-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

Alternatively, you can see change set details in AWS Console. 

6. Execute the change set: 
```
aws cloudformation execute-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

7. View the progress of change set execution:
```
aws cloudformation describe-stacks --stack-name Save2MemriseStack2
```

Status of the stack should be `UPDATE_COMPLETE`. If not, go to CloudFormation in AWS Console and view log messages for this stack. 

8. Delete a change set:
```
aws cloudformation delete-change-set --stack-name Save2MemriseStack2 --change-set-name Save2MemriseStack2ChangeSet
```

NB: If you get the encoded log message like this "API: ec2:RevokeSecurityGroupEgress You are not authorized to perform this operation. Encoded authorization failure message: ...", run this command to decode:
```
aws sts decode-authorization-message --encoded-message <value>
```

### Publish to Chrome Web Store

1. Go to `src/BrowserExts/ChromeExt` directory
2. Increment the version of the extension in `manifest.json`
3. Execute `gulp` 
4. Compress the content of `build` directory as ZIP archive
5. Go to [Chrome Web Store Console](https://chrome.google.com/webstore/developer/dashboard?authuser=1) and sign in as `save2memrise@gmail.com` user
6. Upload the archive

The extension is published at https://chrome.google.com/webstore/detail/save-to-memrise/jedpoleopoehklpioonelookacalmcfk?authuser=1

To publish the extension to the world, you need to re-publish with other visibility options.

### Switch between Green and Blue production environments

To make Green or Blue active environment, update CloudFormation stack:
1. Go to `Save2MemriseStack2.CFTemplate.yml` and set `ActiveProdEnvironment` parameter to `blue` or `green`. Alternatively, you can specify `ActiveProdEnvironment` parameter as command line argument. 
2. Follow instructions in _Update CloudFormation stack in AWS_ section above to apply changes to AWS. 
3. To verify in AWS Console, that traffic was switched successfully: 
3.1. Go to EC2 > Load Balancers > Listeners > Rules. Default routing should forward traffic to the selected environment. 
3.2. Go to Route 53 > Hosted Zones and CloudFront > Distributions and check that `chromeext2.save2memrise.com` is mapped to the proper distribution. 

# Investigate Memrise API 

Sniff traffic of the Memrise mobile app with Packet Capture app on your Android device. Capture app produces *.pcap files that can be stored in `Download` folder of your Android device. If you can't see these files when browsing your device's files with Android File Transfer from your Mac via MTP, rebuild your device's SD index. To rescan your SD, use SD Card Scanner Pro app from Google Play. 

*Update*: Sniff traffic from decks.memrise.com by using Developer tools in Chrome. Memrise REST API is no longer used. 

# Troubleshooting

## Status of AWS::CertificateManager::Certificate is stuck with CREATE_IN_PROGRESS

Check your email and approve a certificate creation for each subdomain. 

