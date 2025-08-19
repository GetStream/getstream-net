#!/usr/bin/env bash

DST_PATH=`pwd`
SOURCE_PATH=../chat

if [ ! -d $SOURCE_PATH ]
then
  echo "cannot find chat path on the parent folder (${SOURCE_PATH}), do you have a copy of the API source?";
  exit 1;
fi

set -ex

# cd in API repo, generate new spec and then generate code from it
( cd $SOURCE_PATH ; make openapi; go run ./cmd/chat-manager openapi generate-client --language dotnet --spec ./releases/v2/feeds-serverside-api.yaml --output $DST_PATH )

# Comment out problematic lines from openapi generation
sed -i '' 's/\[JsonPropertyName("delete_activity")\]/\/\/ [JsonPropertyName("delete_activity")]/' $DST_PATH/src/requests.cs
sed -i '' 's/public DeleteActivityRequest? DeleteActivity { get; set; }/\/\/ public DeleteActivityRequest? DeleteActivity { get; set; }/' $DST_PATH/src/requests.cs
sed -i '' 's/\[JsonPropertyName("delete_message")\]/\/\/ [JsonPropertyName("delete_message")]/' $DST_PATH/src/requests.cs
sed -i '' 's/public DeleteMessageRequest? DeleteMessage { get; set; }/\/\/ public DeleteMessageRequest? DeleteMessage { get; set; }/' $DST_PATH/src/requests.cs
sed -i '' 's/\[JsonPropertyName("delete_reaction")\]/\/\/ [JsonPropertyName("delete_reaction")]/' $DST_PATH/src/requests.cs
sed -i '' 's/public DeleteReactionRequest? DeleteReaction { get; set; }/\/\/ public DeleteReactionRequest? DeleteReaction { get; set; }/' $DST_PATH/src/requests.cs

sed -i '' -e '/\[JsonPropertyName("Role")\]/,+1d' src/models.cs

# Rename Upload methods to match new naming convention
sed -i '' 's/UploadFile/FileUpload/g' $DST_PATH/src/feed.cs $DST_PATH/tests/FeedIntegrationTests.cs $DST_PATH/src/CommonClient.cs
sed -i '' 's/UploadImage/ImageUpload/g' $DST_PATH/src/feed.cs $DST_PATH/tests/FeedIntegrationTests.cs $DST_PATH/src/CommonClient.cs

echo "Generated .NET SDK for feeds in $DST_PATH" 
