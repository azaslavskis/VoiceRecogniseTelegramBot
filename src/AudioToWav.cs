using System;
using System.IO;
using NAudio.Wave;
using Concentus.Oggfile;
using Concentus.Structs;

namespace VoiceRecogniseBot
{  /// <summary>
   /// Provides methods for converting audio files to WAV format.
   /// </summary>
    internal class AudioToWav
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioToWav"/> class.
        /// </summary>
        public AudioToWav() { }

        /// <summary>
        /// Converts an MP3 audio file to WAV format.
        /// </summary>
        /// <param name="mp3FilePath">The path to the input MP3 file.</param>
        /// <returns>The path to the resulting WAV file.</returns>
        public string Mp3AudioToWav(string mp3FilePath)
        {
            int outRate = 16000; // Desired sample rate
            var outFile = Path.GetTempFileName();

            // Create a Mp3FileReader for the input MP3 file
            using (var reader = new Mp3FileReader(mp3FilePath))
            {
                // Create a new WaveFormat with the desired sample rate and the same number of channels as the input
                var outFormat = new WaveFormat(outRate, reader.WaveFormat.Channels);

                // Create a MediaFoundationResampler to resample the audio to the desired format
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    // Optionally, you can set the resampler quality (60 is a good default)
                    // resampler.ResamplerQuality = 60;

                    // Create a WaveFileWriter to write the resampled audio to a WAV file
                    WaveFileWriter.CreateWaveFile(outFile, resampler);
                }
            }

            return outFile;
        }

        /// <summary>
        /// Converts an MP4 audio file to WAV format.
        /// </summary>
        /// <param name="mp4FilePath">The path to the input MP4 file.</param>
        /// <returns>The path to the resulting WAV file.</returns>
        public string Mp4ToWav(string mp4FilePath)
        {
            int outRate = 16000; // Desired sample rate
            var outFile = Path.GetTempFileName();

            // Create a MediaFoundationReader for the input MP4 file
            using (var reader = new MediaFoundationReader(mp4FilePath))
            {
                // Create a new WaveFormat with the desired sample rate and the same number of channels as the input
                var outFormat = new WaveFormat(outRate, reader.WaveFormat.Channels);

                // Create a MediaFoundationResampler to resample the audio to the desired format
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    // Optionally, you can set the resampler quality (60 is a good default)
                    // resampler.ResamplerQuality = 60;

                    // Create a WaveFileWriter to write the resampled audio to a WAV file
                    WaveFileWriter.CreateWaveFile(outFile, resampler);
                }
            }

            return outFile;
        }

        /// <summary>
        /// Converts an Ogg audio file to WAV format.
        /// </summary>
        /// <param name="oggFilePath">The path to the input Ogg file.</param>
        /// <returns>The path to the resulting WAV file.</returns>
        public string OggToWav(string oggFilePath)
        {
            // Generate a temporary WAV file name
            var fileWav = Path.GetTempFileName();

            // Open the input Ogg file for reading
            using (FileStream fileIn = new FileStream(oggFilePath, FileMode.Open))
            using (MemoryStream pcmStream = new MemoryStream())
            {
                // Create an Opus decoder with a sample rate of 16 kHz and 1 channel
                OpusDecoder decoder = OpusDecoder.Create(16000, 1);

                // Create an Opus Ogg stream reader for the input file
                OpusOggReadStream oggIn = new OpusOggReadStream(decoder, fileIn);

                // Decode and convert Ogg packets to PCM
                while (oggIn.HasNextPacket)
                {
                    short[] packet = oggIn.DecodeNextPacket();
                    if (packet != null)
                    {
                        // Convert and write PCM samples to the PCM stream
                        for (int i = 0; i < packet.Length; i++)
                        {
                            var bytes = BitConverter.GetBytes(packet[i]);
                            pcmStream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }

                // Set the position of the PCM stream to the beginning
                pcmStream.Position = 0;

                // Create a raw source wave stream with the PCM data and a sample rate of 16 kHz
                var wavStream = new RawSourceWaveStream(pcmStream, new WaveFormat(16000, 1));

                // Create a sample provider from the WAV stream
                var sampleProvider = wavStream.ToSampleProvider();

                // Write the resampled audio to a 16-bit WAV file
                WaveFileWriter.CreateWaveFile16(fileWav, sampleProvider);
            }

            // Return the path to the resulting WAV file
            return fileWav;
        }

    }
}
