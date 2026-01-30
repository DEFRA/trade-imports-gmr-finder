#!/bin/bash

aws --endpoint-url=http://localhost:4566 sns create-topic \
    --name trade_imports_matched_gmrs

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_matched_gmrs_processor

aws --endpoint-url=http://localhost:4566 sns subscribe \
    --topic-arn arn:aws:sns:eu-west-2:000000000000:trade_imports_matched_gmrs \
    --protocol sqs \
    --notification-endpoint arn:aws:sqs:eu-west-2:000000000000:trade_imports_matched_gmrs_processor \
    --attributes '{"RawMessageDelivery": "true"}'

aws --endpoint-url=http://localhost:4566 \
    sqs create-queue \
    --queue-name trade_imports_data_upserted_gmr_finder


aws --endpoint-url=http://localhost:4566 \
    s3api create-bucket \
    --bucket trade-imports-gmr-finder-search-results \
    --region eu-west-2 \
    --create-bucket-configuration LocationConstraint=eu-west-2

function is_ready() {
    if list_queues="$(aws --endpoint-url=http://localhost:4566 \
    sqs list-queues --region eu-west-2 --query "QueueUrls[?contains(@, 'trade_imports_data_upserted_gmr_finder')] | [0] != null"
    )" && [[ "$list_queues" == "true" ]]; then
        return 0
    fi

    return 1
}

function is_s3_ready() {
    if list_bucket="$(aws --endpoint-url=http://localhost:4566 \
    s3api list-buckets --region eu-west-2 --query "Buckets[?Name=='trade-imports-gmr-finder-search-results'] | [0] != null"
    )" && [[ "$list_bucket" == "true" ]]; then
        return 0
    fi

    return 1
}

while ! is_ready; do
    echo "Waiting until SQS ready"
    sleep 1
done

while ! is_s3_ready; do
    echo "Waiting until S3 ready"
    sleep 1
done


touch /tmp/ready
