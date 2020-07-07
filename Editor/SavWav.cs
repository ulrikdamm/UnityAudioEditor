using System.IO;
using UnityEngine;

// FROM https://gist.github.com/darktable/2317063
public static class SavWav {
	const int HEADER_SIZE = 44;

	public static bool Save(string filename, AudioClip clip, float[] samples) {
		if (!filename.ToLower().EndsWith(".wav")) {
			filename += ".wav";
		}
		
		// Make sure directory exists if user is saving to sub dir.
		Directory.CreateDirectory(Path.GetDirectoryName(filename));

		using (var fileStream = CreateEmpty(filename)) {
			ConvertAndWrite(fileStream, samples);
			WriteHeader(fileStream, clip);
		}

		return true; // TODO: return false if there's a failure saving the file
	}
	
	static FileStream CreateEmpty(string filepath) {
		var fileStream = new FileStream(filepath, FileMode.Create);
	    byte emptyByte = new byte();

	    for(int i = 0; i < HEADER_SIZE; i++) {
	        fileStream.WriteByte(emptyByte);
	    }

		return fileStream;
	}

	static void ConvertAndWrite(FileStream fileStream, float[] samples) {
		// var samples = new float[clip.samples];

		// clip.GetData(samples, 0);

		System.Int16[] intData = new System.Int16[samples.Length];
		//converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

		System.Byte[] bytesData = new System.Byte[samples.Length * 2];
		//bytesData array is twice the size of
		//dataSource array because a float converted in Int16 is 2 bytes.

		int rescaleFactor = 32767; //to convert float to Int16

		for (int i = 0; i<samples.Length; i++) {
			intData[i] = (short) (samples[i] * rescaleFactor);
			System.Byte[] byteArr = new System.Byte[2];
			byteArr = System.BitConverter.GetBytes(intData[i]);
			byteArr.CopyTo(bytesData, i * 2);
		}

		fileStream.Write(bytesData, 0, bytesData.Length);
	}

	static void WriteHeader(FileStream fileStream, AudioClip clip) {
		var hz = clip.frequency;
		var channels = clip.channels;
		var samples = clip.samples;

		fileStream.Seek(0, SeekOrigin.Begin);

		var riff = System.Text.Encoding.UTF8.GetBytes("RIFF");
		fileStream.Write(riff, 0, 4);

		var chunkSize = System.BitConverter.GetBytes(fileStream.Length - 8);
		fileStream.Write(chunkSize, 0, 4);

		var wave = System.Text.Encoding.UTF8.GetBytes("WAVE");
		fileStream.Write(wave, 0, 4);

		var fmt = System.Text.Encoding.UTF8.GetBytes("fmt ");
		fileStream.Write(fmt, 0, 4);

		var subChunk1 = System.BitConverter.GetBytes(16);
		fileStream.Write(subChunk1, 0, 4);

		System.UInt16 two = 2;
		System.UInt16 one = 1;

		var audioFormat = System.BitConverter.GetBytes(one);
		fileStream.Write(audioFormat, 0, 2);

		var numChannels = System.BitConverter.GetBytes(channels);
		fileStream.Write(numChannels, 0, 2);

		var sampleRate = System.BitConverter.GetBytes(hz);
		fileStream.Write(sampleRate, 0, 4);

		var byteRate = System.BitConverter.GetBytes(hz * channels * 2); // sampleRate * bytesPerSample*number of channels, here 44100*2*2
		fileStream.Write(byteRate, 0, 4);

		System.UInt16 blockAlign = (ushort) (channels * 2);
		fileStream.Write(System.BitConverter.GetBytes(blockAlign), 0, 2);

		System.UInt16 bps = 16;
		var bitsPerSample = System.BitConverter.GetBytes(bps);
		fileStream.Write(bitsPerSample, 0, 2);

		var datastring = System.Text.Encoding.UTF8.GetBytes("data");
		fileStream.Write(datastring, 0, 4);

		var subChunk2 = System.BitConverter.GetBytes(samples * channels * 2);
		fileStream.Write(subChunk2, 0, 4);

//		fileStream.Close();
	}
}