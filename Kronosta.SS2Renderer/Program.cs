using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using NAudio;
using NAudio.Wave;

namespace Kronosta.SS2Renderer
{
    public class Program
    {
        public int sampleRate = 44100;
        public int chunksX = 60, chunksY = 60;
        public double chunkLen = 1.0;
        public short[] buffer = null;
        public int chunkLenSamples;
        public int channels = 1;

        public void Init()
        {
            chunkLenSamples = ConvertToSampleTime(chunkLen);
        }

        private void ExitError(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }


        public short[] ConvertIeeeTo16BitPcm(byte[] inputBuffer, int bytesRecorded)
        {
            // 32-bit Float = 4 bytes. 16-bit PCM = 2 bytes. 
            // The target buffer needs to be exactly half the size of the original byte size.
            int sampleCount = bytesRecorded / 4;
            byte[] outputBuffer = new byte[sampleCount * 2];

            // Bind NAudio WaveBuffers to read/write strongly-typed samples from raw bytes
            WaveBuffer sourceWaveBuffer = new WaveBuffer(inputBuffer);
            WaveBuffer destWaveBuffer = new WaveBuffer(outputBuffer);

            for (int i = 0; i < sampleCount; i++)
            {
                // Extract 32-bit float sample (-1.0 to 1.0)
                float sample = sourceWaveBuffer.FloatBuffer[i];

                // Clamp to avoid overflow distortion
                if (sample > 1.0f) sample = 1.0f;
                else if (sample < -1.0f) sample = -1.0f;

                // Scale to 16-bit Int16 range (-32768 to 32767)
                destWaveBuffer.ShortBuffer[i] = (short)(sample * 32767);
            }
            return destWaveBuffer.ShortBuffer;
        }

        private void ReadWav(string filePath)
        {
            byte[] bufferByte = null;

            using (var reader = new AudioFileReader(filePath))
            {
                channels = reader.WaveFormat.Channels;
                bufferByte = new byte[reader.Length];
                int samplesRead = reader.Read(bufferByte, 0, bufferByte.Length);
                if (samplesRead != reader.Length)
                    ExitError("Could not read full file.");
            };

            buffer = ConvertIeeeTo16BitPcm(bufferByte, bufferByte.Length);
        }

        private void WriteWav(string outPath, short[] samples)
        {
            WaveFormat format = new WaveFormat(sampleRate, 16, channels);
            byte[] rawAudioBytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            
            {
                rawAudioBytes[i * 2] = (byte)(samples[i] & 0xFF);
                rawAudioBytes[i * 2 + 1] = (byte)(samples[i] >> 8);
            }

            using (var writer = new WaveFileWriter(outPath, format))
            {
                writer.Write(rawAudioBytes, 0, rawAudioBytes.Length);
            }
        }

        private int ConvertToSampleTime(double time) => (int)((time + 0.0000000001) * sampleRate); // get past some amount of floating point error
        private short ConvertToSampleAmplitude(double amp) => (short)(amp * 65536);


        public short GetSample(int x, int y, int channel)
        {
            int localx = x % chunkLenSamples;
            int localy = y % chunkLenSamples;
            int chunkx = x / chunkLenSamples;
            int chunky = y / chunkLenSamples;

            short samplex = buffer[(chunky * channels * chunkLenSamples * chunksX) + (chunkx * channels * chunkLenSamples) + (localx * channels) + channel];
            // the x chunk audio and y chunk audio always corresponds to exactly the same range of raw audio, just with different subchunk offset.
            short sampley = buffer[(chunky * channels * chunkLenSamples * chunksX) + (chunkx * channels * chunkLenSamples) + (localy * channels) + channel];

            return (short)((samplex + sampley) / 2);
        }

        public short[] CreateLine(double startx, double starty, double endx, double endy)
        {
            double distx = endx - startx;
            double disty = endy - starty;
            
            double lengthx = endx - startx;
            double lengthy = endy - starty;
            double length = Math.Sqrt(lengthx * lengthx + lengthy * lengthy);

            double stepx = distx / length;
            double stepy = disty / length;

            double posx = startx;
            double posy = starty;

            int lengthSamples = ConvertToSampleTime(length) * channels;
            short[] samples = new short[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                samples[i] = GetSample(ConvertToSampleTime(posx), ConvertToSampleTime(posy), i % channels);

                if (i % channels == channels - 1) {
                    posx += stepx / sampleRate;
                    posy += stepy / sampleRate;
                }

                if (i / channels % 5000 == 0) Console.WriteLine($"Finished sample {i}; pos [{posx}, {posy}].");
            }

            return samples;
        }

        public short[] CreateVertical()
        {
            double lenx = chunkLen * chunksX;
            double leny = chunkLen * chunksY;
            List<short[]> lines = new List<short[]>();

            // Vertical playback
            for (double i = 0; i < lenx; i += chunkLen)
                lines.Add(CreateLine(i, 0.0, i, leny));

            // Join all samples together
            return lines.Aggregate((arr1, arr2) => arr1.Concat(arr2).ToArray());
        }

        public short[] CreateExperience()
        {
            double lenx = chunkLen * chunksX;
            double leny = chunkLen * chunksY;
            List<short[]> lines = new List<short[]>();

            // Horizontal playback
            for (double i = 0; i < leny; i += chunkLen)
                lines.Add(CreateLine(0.0, i, lenx, i));

            // Vertical playback
            for (double i = 0; i < lenx; i += chunkLen)
                lines.Add(CreateLine(i, 0.0, i, leny));

            // Main diagonal
            lines.Add(CreateLine(0.0, 0.0, lenx, lenx));

            // Snaking across the timeplane
            // Left and bottom edge
            double xEdgeCounter = chunkLen;
            double yEdgeScale = leny / lenx; // Handle rectangles
            while (xEdgeCounter <= lenx)
            {
                /*
                For a 3x3 second clip with 1 second chunks:
                0.0, 0.0, 1.0, 0.0
                1.0, 0.0, 0.0, 1.0
                0.0, 1.0, 2.0, 0.0
                2.0, 0.0, 0.0, 2.0
                0.0, 2.0, 3.0, 0.0
                3.0, 0.0, 0.0, 3.0

                */
                lines.Add(CreateLine(0.0, (xEdgeCounter - chunkLen) * yEdgeScale, xEdgeCounter, 0.0));
                lines.Add(CreateLine(xEdgeCounter, 0.0, 0.0, xEdgeCounter * yEdgeScale));
                xEdgeCounter += chunkLen;
            }
            // Right and top edge
            double yEdgeCounter = chunkLen;
            double xEdgeScale = lenx / leny; // Handle rectangles
            while (yEdgeCounter <= leny)
            {
                /*
                For a 3x3 second clip with 1 second chunks:
                0.0, 3.0, 3.0, 1.0
                3.0, 1.0, 1.0, 3.0
                1.0, 3.0, 3.0, 2.0
                3.0, 2.0, 2.0, 3.0
                2.0, 3.0, 3.0, 3.0
                3.0, 3.0, 3.0, 3.0 (will produce a zero-length sample array without error)

                */
                lines.Add(CreateLine((yEdgeCounter - chunkLen) * xEdgeScale, leny, lenx, yEdgeCounter));
                lines.Add(CreateLine(lenx, yEdgeCounter, yEdgeCounter * xEdgeScale, leny));
                yEdgeCounter += chunkLen;
            }

            // Join all samples together
            return lines.Aggregate((arr1, arr2) => arr1.Concat(arr2).ToArray());
        }

        public short[] CreateRandom(int iterations)
        {
            double lenx = chunkLen * chunksX;
            double leny = chunkLen * chunksY;
            var random = new Random((int)DateTime.Now.Ticks);
            List<short[]> lines = new List<short[]>();

            double xprev = random.NextDouble() * lenx;
            double yprev = random.NextDouble() * leny;
            for (int i = 0; i < iterations; i++)
            {
                double x = random.NextDouble() * lenx;
                double y = random.NextDouble() * leny;
                lines.Add(CreateLine(xprev, xprev, x, y));
                xprev = x;
                yprev = y;
            }

            // Join all samples together
            return lines.Aggregate((arr1, arr2) => arr1.Concat(arr2).ToArray());
        }

        public void InstanceMain(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "chunkparams":
                        {
                            string paramz = args[++i];
                            string[] paramsArray = paramz.Split(",");
                            chunksX = int.Parse(paramsArray[0]);
                            chunksY = int.Parse(paramsArray[1]);
                            chunkLen = double.Parse(paramsArray[2]);
                            Init();
                            break;
                        }
                    case "line":
                        {
                            string filePath = args[++i];
                            string lineDataStr = args[++i];
                            string outPath = args[++i];
                            string[] lineData = lineDataStr.Split(',');
                            double startx = double.Parse(lineData[0]);
                            double starty = double.Parse(lineData[1]);
                            double endx = double.Parse(lineData[2]);
                            double endy = double.Parse(lineData[3]);
                            Init();
                            ReadWav(filePath);
                            short[] result = CreateLine(startx, starty, endx, endy);
                            WriteWav(outPath, result);
                            break;
                        }
                    case "experience":
                        {
                            string filePath = args[++i];
                            string outPath = args[++i];
                            Init();
                            ReadWav(filePath);
                            short[] result = CreateExperience();
                            WriteWav(outPath, result);
                            break;
                        }
                    case "vertical":
                        {
                            string filePath = args[++i];
                            string outPath = args[++i];
                            Init();
                            ReadWav(filePath);
                            short[] result = CreateVertical();
                            WriteWav(outPath, result);
                            break;
                        }
                    case "random":
                        {
                            string filePath = args[++i];
                            int iterations = int.Parse(args[++i]);
                            if (iterations <= 0) ExitError("There must be at least 1 iteration.");
                            string outPath = args[++i];
                            Init();
                            ReadWav(filePath);
                            short[] result = CreateRandom(iterations);
                            WriteWav(outPath, result);
                            break;
                        }
                }
            }

        }

        public static void Main(string[] args)
        {
            Program program = new Program();
            program.InstanceMain(args);
        }
    }
}
