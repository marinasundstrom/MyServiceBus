#!/usr/bin/env sh
set -eu

region="${AWS_REGION:-}"
if [ -z "$region" ]; then
  echo "Set AWS_REGION to the AWS region used for the acceptance test." >&2
  exit 1
fi

identity="$(aws sts get-caller-identity --query Arn --output text)"
case "$identity" in
  *:root)
    echo "Refusing to provision test resources with AWS account root credentials." >&2
    echo "Authenticate with an IAM Identity Center or assumed-role profile, then retry." >&2
    exit 1
    ;;
esac

echo "Running Amazon SQS/SNS cloud acceptance in $region as $identity"
RUN_AMAZON_SQS_CLOUD_TESTS=1 dotnet test \
  test/MyServiceBus.AmazonSqs.Tests/MyServiceBus.AmazonSqs.Tests.csproj \
  --filter FullyQualifiedName~AmazonSqsCloudTests
RUN_AMAZON_SQS_CLOUD_TESTS=1 gradle :myservicebus-amazon-sqs:test --rerun-tasks \
  --tests com.myservicebus.amazon.sqs.AmazonSqsCloudTest
