#!/bin/sh

name=MustardLogo

for size in 8 16 22 24 32 36 42 48 52 64 72 96 128 192 256; do
        convert "$name".png -resize "$size"x"$size" "$name""$size".png
done

