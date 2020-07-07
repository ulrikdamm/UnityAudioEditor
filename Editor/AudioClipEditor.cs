using UnityEngine;
using UnityEditor;
using Stopwatch = System.Diagnostics.Stopwatch;

public class AudioData {
	public float[] samples;
	public int channels;
	
	public AudioData(int sampleCount, int channels) {
		samples = new float[sampleCount * channels];
		this.channels = channels;
	}
	
	public AudioData(AudioClip clip) {
		samples = new float[clip.samples * clip.channels];
		clip.GetData(samples, offsetSamples: 0);
		channels = clip.channels;
	}
	
	public int sampleCount => samples.Length / channels;
	
	public float getSample(int index, int channel) {
		return samples[index * channels + channel];
	}
	
	public void setSample(int index, int channel, float value) {
		samples[index * channels + channel] = value;
	}
	
	public int arrayIndex(int sample, int channel) {
		return sample * channels + channel;
	}
}

class AudioUtilBindings {
	static System.Type AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
	
	public static void stopAllClips() => AudioUtil.GetMethod("StopAllClips").Invoke(null, null);
	public static void playClip(AudioClip clip, bool looping = false) => AudioUtil.GetMethod("PlayClip").Invoke(null, new object[] { clip, 0, looping });
	public static bool isClipPlaying(AudioClip clip) => (bool)AudioUtil.GetMethod("IsClipPlaying").Invoke(null, new object[] { clip });
	public static float getDuration(AudioClip clip) => (float)AudioUtil.GetMethod("GetDuration").Invoke(null, new object[] { clip });
	public static float getClipPosition(AudioClip clip) => (float)AudioUtil.GetMethod("GetClipPosition").Invoke(null, new object[] { clip });
	public static int getClipSamplePosition(AudioClip clip) => (int)AudioUtil.GetMethod("GetClipSamplePosition").Invoke(null, new object[] { clip });
	public static void setClipSamplePosition(AudioClip clip, int sample) => AudioUtil.GetMethod("SetClipSamplePosition").Invoke(null, new object[] { clip, sample });
	public static void pauseClip(AudioClip clip) => AudioUtil.GetMethod("PauseClip").Invoke(null, new object[] { clip });
	public static void resumeClip(AudioClip clip) => AudioUtil.GetMethod("ResumeClip").Invoke(null, new object[] { clip });
}

public class AudioClipEditor : EditorWindow {
	[MenuItem("Window/Audio Clip Editor")]
	static void showWindow() {
		var window = EditorWindow.GetWindow(typeof(AudioClipEditor));
		window.Show();
	}
	
	int offset;
	bool isPaused;
	
	AudioClip instancedAudioClip;
	AudioData currentAudioData;
	int audioPlayerOffset;
	
	float playbackVolume = 1;
	float scale = 1;
	long? lastScrollTime;
	Vector2 scrollPos;
	
	VolumeGraph volumeGraph;
	AudioWaveformRenderer waveformRenderer;
	SelectionRenderer selectionRenderer;
	
	void OnEnable() {
		volumeGraph = new VolumeGraph();
		selectionRenderer = new SelectionRenderer();
		waveformRenderer = new AudioWaveformRenderer(volumeGraph.volumeAtTime, () => currentAudioData);
		selectionRenderer.selectionChanged += onSelectionRangeChanged;
		
		wantsMouseMove = true;
		EditorApplication.update += onUpdate;
	}
	
	void OnDisable() {
		EditorApplication.update -= onUpdate;
	}
	
	void onUpdate() {
		if (lastScrollTime.HasValue && lastScrollTime.Value + 1_000_000 < Stopwatch.GetTimestamp()) {
			lastScrollTime = null;
			waveformRenderer.clearMinMaxCache();
			Repaint();
		}
	}
	
	void OnSelectionChange() {
		instancedAudioClip = null;
		clearCaches();
		Repaint();
	}
	
	void clearCaches() {
		AudioUtilBindings.stopAllClips();
		isPaused = false;
		waveformRenderer.clearMinMaxCache();
		selectionRenderer.clearCaches();
		volumeGraph = new VolumeGraph();
	}
	
	void OnGUI() {
		var clip = selectedAudioClip;
		var text = (clip != null ? clip.name : "No audio clip selected");
		
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("Audio editor: " + text);
		
		if (clip != null && selectionRenderer.hasSelection) {
			if (GUILayout.Button("Fade in")) { volumeGraph.applyFadeIn(selectionRenderer.selectionFrom, selectionRenderer.selectionTo); Repaint(); }
			if (GUILayout.Button("Fade out")) { volumeGraph.applyFadeOut(selectionRenderer.selectionFrom, selectionRenderer.selectionTo); Repaint(); };
			if (GUILayout.Button("Silence")) { volumeGraph.applySilence(selectionRenderer.selectionFrom, selectionRenderer.selectionTo); Repaint(); }
			
			if (selectionRenderer.hasSelectionRange) {
				if (GUILayout.Button("Crop")) { cropAudio(selectionRenderer.selectionFrom, selectionRenderer.selectionTo); Repaint(); }
			} else {
				if (GUILayout.Button("crop from")) { cropAudio(selectionRenderer.selectionFrom, to: 1); Repaint(); }
				if (GUILayout.Button("crop to")) { cropAudio(from: 0, selectionRenderer.selectionTo); Repaint(); }
			}
		}
		
		if (GUILayout.Button("Export")) { exportAudio(); Repaint(); }
		
		if (clip != null) {
			if (AudioUtilBindings.isClipPlaying(clip)) {
				if (GUILayout.Button("Stop")) {
					AudioUtilBindings.stopAllClips();
					isPaused = false;
				}
				
				if (!isPaused) {
					if (GUILayout.Button("Pause")) {
						isPaused = true;
						AudioUtilBindings.pauseClip(clip);
					}
				} else {
					if (GUILayout.Button("Play")) {
						isPaused = false;
						AudioUtilBindings.resumeClip(clip);
					}
				}
			} else {
				if (GUILayout.Button("Play")) {
					isPaused = false;
					AudioUtilBindings.stopAllClips();
					AudioUtilBindings.playClip(clip);
					
					if (selectionRenderer.hasSelection) {
						var sampleFrom = Mathf.FloorToInt(selectionRenderer.selectionFrom * currentAudioData.sampleCount);
						AudioUtilBindings.setClipSamplePosition(clip, sampleFrom);
					}
				}
			}
			
			EditorGUILayout.PrefixLabel("Playback volume");
			playbackVolume = EditorGUILayout.Slider(playbackVolume, 0, 1);
		}
		
		EditorGUILayout.EndHorizontal();
		
		if (clip == null) { return; }
		
		if (AudioUtilBindings.isClipPlaying(clip) && selectionRenderer.hasSelectionRange) {
			var currentSample = AudioUtilBindings.getClipSamplePosition(clip);
			var sampleFrom = Mathf.FloorToInt(selectionRenderer.selectionFrom * currentAudioData.sampleCount);
			var sampleTo = Mathf.FloorToInt(selectionRenderer.selectionTo * currentAudioData.sampleCount);
			
			if (currentSample >= sampleTo) {
				AudioUtilBindings.stopAllClips();
			}
		}
		
		var mouseInScrollView = contentRect.containsPoint(Event.current.mousePosition);
		
		scrollPos = GUI.BeginScrollView(contentRect, scrollPos, scrollRect, alwaysShowHorizontal: true, alwaysShowVertical: false);
		
		if (volumeGraph.handleMouseEvent(Event.current, channelRect(0, currentAudioData.channels))) {
			Event.current.Use();
			Repaint();
		} else if (selectionRenderer.handleEvent(Event.current, scrollRect)) {
			Event.current.Use();
			Repaint();
		} else {
			switch (Event.current.type) {
				case EventType.Repaint: repaint(); break;
				case EventType.ScrollWheel:
					if (mouseInScrollView) {
						// var mouseBefore = Mathf.InverseLerp(0, scrollRect.width, Event.current.mousePosition.x);
						scale = Mathf.Clamp(scale - Event.current.delta.y * 0.1f, min: 1, max: 40);
						// var mouseAfter = Mathf.InverseLerp(0, scrollRect.width, Event.current.mousePosition.x);
						// Debug.Log($"Before: {mouseBefore}, after: {mouseAfter}");
						// scrollPos.x += (mouseAfter - mouseBefore);
						
						lastScrollTime = Stopwatch.GetTimestamp();
						Repaint();
						Event.current.Use();
					}
					break;
				case EventType.KeyDown:
					if (Event.current.keyCode == KeyCode.Space) {
						if (clip == null) { break; }
						
						Event.current.Use();
						if (AudioUtilBindings.isClipPlaying(clip)) {
							if (!isPaused) {
								isPaused = true;
								AudioUtilBindings.pauseClip(clip);
							} else {
								isPaused = false;
								AudioUtilBindings.resumeClip(clip);
							}
						} else {
							isPaused = false;
							AudioUtilBindings.stopAllClips();
							AudioUtilBindings.playClip(clip);
							
							if (selectionRenderer.hasSelection) {
								var sampleFrom = Mathf.FloorToInt(selectionRenderer.selectionFrom * currentAudioData.sampleCount);
								AudioUtilBindings.setClipSamplePosition(clip, sampleFrom);
							}
						}
					} else if (Event.current.keyCode == KeyCode.LeftArrow) {
						Event.current.Use();
						selectionRenderer.moveSelection(seconds: -1, clipLength: clip.length, moveTo: (Event.current.modifiers & EventModifiers.Shift) == 0);
						Repaint();
					} else if (Event.current.keyCode == KeyCode.RightArrow) {
						Event.current.Use();
						selectionRenderer.moveSelection(seconds: 1, clipLength: clip.length, moveTo: (Event.current.modifiers & EventModifiers.Shift) == 0);
						Repaint();
					}
					break;
			}
		}
		
		GUI.EndScrollView();
	}
	
	void onSelectionRangeChanged() {
		var clip = selectedAudioClip;
		if (clip == null) { return; }
		
		if (AudioUtilBindings.isClipPlaying(clip) && isPaused) {
			AudioUtilBindings.stopAllClips();
		}
	}
	
	Rect contentRect => new Rect(20, 30, position.width - 40, (position.height - 20) - 30);
	Rect scrollRect => new Rect(0, 0, contentRect.width * scale, contentRect.height - (GUI.skin.verticalScrollbar.lineHeight + 2));
	
	Rect channelRect(int channel, int channels) {
		var rect = new Rect(Vector2.zero, contentRect.size);
		var spacing = 10;
		
		var height = (rect.height - spacing * (channels - 1)) / channels;
		return new Rect(rect.x, rect.y + (height + spacing) * channel, rect.width * scale, height);
	}
	
	void repaint() {
		var clip = selectedAudioClip;
		if (clip == null) { return; }
		
		offset = AudioUtilBindings.getClipSamplePosition(clip);
		
		for (var i = 0; i < clip.channels; i++) {
			drawChannel(channelRect(i, clip.channels), channel: i);
		}
		
		selectionRenderer.drawHover(scrollRect, Color.red);
		selectionRenderer.drawSelection(scrollRect, Color.red);
		
		if (AudioUtilBindings.isClipPlaying(clip)) {
			var currentSample = AudioUtilBindings.getClipSamplePosition(clip);
			var xpos = (currentSample / (float)currentAudioData.sampleCount) * scrollRect.width;
			EditorGUI.DrawRect(new Rect(xpos, 0, 2, scrollRect.height), Color.green);
			Repaint();
		}
		
		volumeGraph.drawMouseHover(channelRect(0, currentAudioData.channels), (Texture)AssetDatabase.LoadAssetAtPath("Assets/Editor/Handle.png", typeof(Texture)));
	}
	
	void drawChannel(Rect rect, int channel) {
		EditorGUI.DrawRect(rect, defaultBackgroundColor);
		var curveRect = new Rect(rect.x, rect.y, rect.width, rect.height);
		var volumeRect = new Rect(rect.x, rect.y, rect.width, rect.height / 2);
		
		volumeGraph.drawVolumeFill(volumeRect, new Color(0.2f, 0.2f, 0.2f));
		
		waveformRenderer.drawWaveform(curveRect, channel, visibleRange: new Vector2(scrollPos.x / (contentRect.width * scale), (scrollPos.x + contentRect.width) / (contentRect.width * scale)));
		
		volumeGraph.drawVolumeLine(volumeRect, new Color(0.1f, 0.1f, 0.1f));
		volumeGraph.drawVolumeLine(volumeRect, new Color(0.1f, 0.1f, 0.1f), mirrored: true);
		volumeGraph.drawVolumeHandles(volumeRect, (Texture)AssetDatabase.LoadAssetAtPath("Assets/Editor/Handle.png", typeof(Texture)));
	}
	
	AudioClip originalSelectedAudioClip {
		get {
			if (Selection.activeObject == null) { return null; }
			if (!(Selection.activeObject is AudioClip)) { return null; }
			
			var clip = (AudioClip)Selection.activeObject;
			return clip;
		}
	}
	
	AudioClip selectedAudioClip {
		get {
			if (instancedAudioClip != null) { return instancedAudioClip; }
			
			var clip = originalSelectedAudioClip;
			if (clip == null) { return null; }
			
			currentAudioData = new AudioData(clip);
			instancedAudioClip = AudioClip.Create(clip.name + " (Clone)", clip.samples, clip.channels, clip.frequency, stream: false, audioReaderCallback, audioPositionCallback);
			return instancedAudioClip;
		}
	}
	
	void audioPositionCallback(int setPosition) {
		audioPlayerOffset = setPosition;
	}
	
	void audioReaderCallback(float[] fillMeWithSamplesPlz) {
		for (var i = 0; i < fillMeWithSamplesPlz.Length; i++) {
			var sample = (audioPlayerOffset + i) / currentAudioData.channels;
			var progress = sample / (float)currentAudioData.sampleCount;
			fillMeWithSamplesPlz[i] = currentAudioData.samples[audioPlayerOffset + i] * playbackVolume * volumeGraph.volumeAtTime(progress);
		}
		
		audioPlayerOffset += fillMeWithSamplesPlz.Length;
	}
	
	void cropAudio(float from, float to) {
		volumeGraph.crop(Mathf.Min(from, to), Mathf.Max(from, to));
		
		var sampleFrom = Mathf.FloorToInt(Mathf.Min(from, to) * currentAudioData.sampleCount);
		var sampleTo = Mathf.FloorToInt(Mathf.Max(from, to) * currentAudioData.sampleCount);
		
		var indexFrom = currentAudioData.arrayIndex(sampleFrom, channel: 0);
		var indexTo = currentAudioData.arrayIndex(sampleTo, channel: 0);
		
		var samples = new float[indexTo - indexFrom];
		System.Array.Copy(currentAudioData.samples, indexFrom, samples, 0, samples.Length);
		currentAudioData.samples = samples;
		
		audioPlayerOffset = 0;
		instancedAudioClip = AudioClip.Create(instancedAudioClip.name, samples.Length / instancedAudioClip.channels, instancedAudioClip.channels, instancedAudioClip.frequency, stream: true, audioReaderCallback, audioPositionCallback);
		
		selectionRenderer.clearCaches();
		waveformRenderer.clearMinMaxCache();
	}
	
	void exportAudio() {
		var path = AssetDatabase.GetAssetPath(originalSelectedAudioClip);
		var directory = System.IO.Path.GetDirectoryName(path);
		var originalName = System.IO.Path.GetFileNameWithoutExtension(path);
		var filename = originalName + " (Edited).wav";
		var completePath = System.IO.Path.Combine(directory, filename);
		SavWav.Save(completePath, instancedAudioClip, currentAudioData.samples);
		AssetDatabase.Refresh();
	}
	
	Color defaultBackgroundColor { get {
		var kViewBackgroundIntensity = EditorGUIUtility.isProSkin ? 0.22f : 0.76f;
		return new Color(kViewBackgroundIntensity, kViewBackgroundIntensity, kViewBackgroundIntensity, 1f);
	} }
}

public static class RectExtensions {
	public static bool containsPoint(this Rect rect, Vector2 point) => point.x > rect.xMin && point.x < rect.xMax && point.y > rect.yMin && point.y < rect.yMax;
	public static Vector2 pointInRect(this Rect rect, Vector2 point) => new Vector2(point.x - rect.x, point.y - rect.y);
}

public static class VectorExtensions {
	public static Vector2? pointOnLine(this Vector2 point, Vector2 from, Vector2 to, float maxDistance) {
		var distance = Vector2.Distance(from, to);
		if (distance < 0.01f) {
			distance = Vector2.Distance(from, point);
			return (distance < maxDistance ? (Vector2?)from : null);
		}
		
		var progress = Vector2.Dot(point - from, to - from) / (distance * distance);
		if (progress < 0 || progress > 1) { return null; }
		
		var projection = from + progress * (to - from);
		if (Vector3.Distance(projection, point) > maxDistance) { return null; }
		return projection;
	}
}
