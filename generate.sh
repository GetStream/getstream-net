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
( cd $SOURCE_PATH ; make openapi ; go run ./cmd/chat-manager openapi generate-client --language dotnet --spec ./releases/v2/feeds-serverside-api.yaml --output $DST_PATH )

echo "Generated .NET SDK for feeds in $DST_PATH" 
