This is the renderer for my songs "SS^2" and "E2 goccaug E4 cw Arctakkurus" \[`Arctakkurus.wav`\], songs in two dimensions of time
that have to be dynamically generated in order to be reasonably stored.

# How to use
First go to the releases tab and download "SS^2_Package-1.0". It contains the program plus the raw audio chunk files
for the two songs listed above, and a few sample commands.

The command-line arguments are a bit weird: they are read left-to-right in sequence.

The commands are as follows:
- `chunkparams [number of chunks on x],[number of chunks on y],[length of a chunks in seconds, can have decimals]`: Sets global parameters for the chunks.
  This is necessary before rendering anything with a 2D song that isn't exactly like SS^2 (60x60 1.0 second chunks). For example,
  E2 goccaug E4 cw Arctakkurus uses `16,16,0.3`
- `line [input wav file] [start x],[start y],[end x],[end y] [output wav file]`: Traces a line segment through the song and outputs what is on that line.
- `vertical [input wav file] [output wav file]`: Goes through every chunk vertically (for horizontally, just listen to the raw audio chunks file)
- `experience [input wav file] [output wav file]`: Outputs one audio file that plays the audio horizontally, vertically, along the main diagonal, and
  snaking back and forth. It has some issues currently but does work okay enough; (small clicks occur at chunk boundaries and it adds a little
  extra content after the horizontal and vertical segments).
- `random [input wav file] [n] [output]`: Traces n connected line segments darting randomly across the timeplane.
