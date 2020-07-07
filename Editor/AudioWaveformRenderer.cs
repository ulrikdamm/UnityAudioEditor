using UnityEngine;
using UnityEditor;

public class AudioWaveformRenderer {
	System.Func<float, float> volumeAtTime;
	System.Func<AudioData> getAudioData;
	
	public AudioWaveformRenderer(System.Func<float, float> volumeAtTime, System.Func<AudioData> getAudioData) {
		this.volumeAtTime = volumeAtTime;
		this.getAudioData = getAudioData;
	}
	
	public void drawWaveform(Rect rect, int channel, Vector2 visibleRange) {
		var minX = rect.x + rect.width * visibleRange.x;
		var maxX = rect.x + rect.width * visibleRange.y;
		var visibleRect = new Rect(minX, rect.y, maxX - minX, rect.height);
		
		AudioCurveRendering.AudioMinMaxCurveAndColorEvaluator dlg = delegate(float x, out Color col, out float minValue, out float maxValue) {
			col = Color.yellow;
			
			x = Mathf.InverseLerp(rect.xMin, rect.xMax, Mathf.Lerp(minX, maxX, x));
			var divisions = Mathf.FloorToInt(rect.width);
			var groupIndex = Mathf.Clamp(Mathf.FloorToInt(x * divisions), min: 0, max: divisions - 1);
			minMaxInInterval(groupIndex, divisions, channel, out minValue, out maxValue);
		};
		
		AudioCurveRendering.DrawMinMaxFilledCurve(visibleRect, dlg);
	}
	
	struct MinMax { public bool cached; public float min; public float max; }
	MinMax[] minMaxCache;
	
	public void clearMinMaxCache() => minMaxCache = null;
	
	void minMaxInInterval(int groupIndex, int divisions, int channel, out float min, out float max) {
		var audioData = getAudioData();
		
		if (minMaxCache == null/* || minMaxCache.Length != divisions * audioData.channels*/) {
			minMaxCache = new MinMax[divisions * audioData.channels];
		}
		
		if (minMaxCache.Length != divisions * audioData.channels) {
			var newDivisions = minMaxCache.Length / audioData.channels;
			groupIndex = Mathf.FloorToInt((groupIndex / (float)divisions) * newDivisions);
			divisions = newDivisions;
		}
		
		var channelGroupIndex = groupIndex * audioData.channels + channel;
		
		if (!minMaxCache[channelGroupIndex].cached) {
			minMaxCache[channelGroupIndex] = calculateMinMaxInInterval(groupIndex, divisions, channel);
		}
		
		var minMax = minMaxCache[channelGroupIndex];
		var volume = volumeAtTime(groupIndex / (float)divisions);
		min = minMax.min * volume;
		max = minMax.max * volume;
	}
	
	MinMax calculateMinMaxInInterval(int groupIndex, int divisions, int channel) {
		var audioData = getAudioData();
		
		var min = 1f;
		var max = -1f;
		
		var fromSample = (audioData.sampleCount / divisions) * groupIndex;
		var toSample = (audioData.sampleCount / divisions) * (groupIndex + 1);
		
		for (var i = fromSample; i < toSample; i++) {
			var value = audioData.getSample(i, channel);
			if (value < min) { min = value; }
			if (value > max) { max = value; }
		}
		
		return new MinMax { cached = true, min = min, max = max };
	}
}
