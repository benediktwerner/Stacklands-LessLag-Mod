#!/bin/sh

rm -r tmp release.zip

mkdir -p tmp/patchers
mkdir -p tmp/plugins

cp src_patcher/bin/Release/net35/LessLag.Patcher.dll tmp/patchers/
cp src_plugin/bin/Release/net4.6.1/LessLag.dll tmp/plugins/
cp manifest.json tmp/
cp icon.png tmp/
cp Readme.md tmp/README.md

cd tmp
zip -r release *
cd -

mv tmp/release.zip ./
rm -r tmp
