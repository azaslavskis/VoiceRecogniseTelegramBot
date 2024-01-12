using NAudio.Wave;
using NAudio.Vorbis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Concentus.Oggfile;
using Concentus.Structs;
namespace VoiceRecogniseBot
{
    internal class AudioToWav
    {
        public AudioToWav() { }
        public string Mp3AudioToWav(string name)
        {
            int outRate = 16000;
            var outFile = Path.GetTempFileName();
            using (var reader = new Mp3FileReader(name))
            {
                var outFormat = new WaveFormat(outRate, reader.WaveFormat.Channels);
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    // resampler.ResamplerQuality = 60;
                    WaveFileWriter.CreateWaveFile(outFile, resampler);
                }
                return outFile;
            }
        }

        public string OggToWav(string name)
        {


            var fileOgg = name;
            var fileWav = Path.GetTempFileName();

            using (FileStream fileIn = new FileStream(fileOgg, FileMode.Open))
            using (MemoryStream pcmStream = new MemoryStream())
            {
                OpusDecoder decoder = OpusDecoder.Create(16000, 1);
                OpusOggReadStream oggIn = new OpusOggReadStream(decoder, fileIn);
                while (oggIn.HasNextPacket)
                {
                    short[] packet = oggIn.DecodeNextPacket();
                    if (packet != null)
                    {
                        for (int i = 0; i < packet.Length; i++)
                        {
                            var bytes = BitConverter.GetBytes(packet[i]);
                            pcmStream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
                pcmStream.Position = 0;
                var wavStream = new RawSourceWaveStream(pcmStream, new WaveFormat(16000, 1));
                var sampleProvider = wavStream.ToSampleProvider();
                WaveFileWriter.CreateWaveFile16(fileWav, sampleProvider);
                return fileWav;
            }
        }
    }
}
