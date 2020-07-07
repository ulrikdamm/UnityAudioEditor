using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class VolumeGraph {
	struct MovingVolumePoint {
		public int index;
		public Vector2 startPosition;
		public bool hasStartedDragging;
	};
	
	MovingVolumePoint? movingVolumePoint;
	
	struct VolumePoint { public float time; public float volume; }
	List<VolumePoint> volumePoints = new List<VolumePoint>();
	
	public void drawVolumeLine(Rect rect, Color color, bool mirrored = false) {
		GL.Begin(GL.LINES);
		GL.Color(color);
		
		if (mirrored) {
			rect.y += rect.height * 2;
			rect.height = -rect.height;
		}
		
		if (volumePoints.Count == 0) {
			drawGLLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y));
		} else {
			for (var i = 0; i < volumePoints.Count; i++) {
				var point = volumePoints[i];
				Vector2 linePoint;
				
				if (i == 0) {
					linePoint = new Vector2(rect.x, rect.y + rect.height * (1 - point.volume));
				} else {
					var prevPoint = volumePoints[i - 1];
					linePoint = new Vector2(rect.x + rect.width * prevPoint.time, rect.y + rect.height * (1 - prevPoint.volume));
				}
				
				drawGLLine(new Vector2(rect.x + rect.width * point.time, rect.y + rect.height * (1 - point.volume)), linePoint);
			}
			
			var lastPoint = volumePoints[volumePoints.Count - 1];
			drawGLLine(
				from: new Vector2(rect.x + rect.width * lastPoint.time, rect.y + rect.height * (1 - lastPoint.volume)),
				to: new Vector2(rect.x + rect.width, rect.y + rect.height * (1 - lastPoint.volume))
			);
		}
		
		GL.End();
	}
	
	void drawGLLine(Vector2 from, Vector2 to) {
		GL.Vertex(new Vector3(from.x, from.y, 0));
		GL.Vertex(new Vector3(to.x, to.y, 0));
	}
	
	public void drawVolumeFill(Rect rect, Color color) {
		GL.Begin(GL.QUADS);
		GL.Color(color);
		
		if (volumePoints.Count == 0) {
			GL.Vertex(new Vector3(rect.x, rect.y, 0));
			GL.Vertex(new Vector3(rect.x, rect.y + rect.height * 2, 0));
			GL.Vertex(new Vector3(rect.x + rect.width, rect.y + rect.height * 2, 0));
			GL.Vertex(new Vector3(rect.x + rect.width, rect.y, 0));
		} else {
			for (var i = 0; i < volumePoints.Count; i++) {
				var point = volumePoints[i];
				
				float firstHeight;
				if (i == 0) {
					firstHeight = rect.height * (1 - point.volume);
					GL.Vertex(new Vector3(rect.x, rect.y + firstHeight, 0));
					GL.Vertex(new Vector3(rect.x, rect.y + (rect.height * 2 - firstHeight), 0));
				} else {
					var prevPoint = volumePoints[i - 1];
					firstHeight = rect.height * (1 - volumePoints[i - 1].volume);
					GL.Vertex(new Vector3(rect.x + rect.width * prevPoint.time, rect.y + firstHeight, 0));
					GL.Vertex(new Vector3(rect.x + rect.width * prevPoint.time, rect.y + (rect.height * 2 - firstHeight), 0));
				}
				
				var secondHeight = rect.height * (1 - point.volume);
				GL.Vertex(new Vector3(rect.x + rect.width * point.time, rect.y + (rect.height * 2 - secondHeight), 0));
				GL.Vertex(new Vector3(rect.x + rect.width * point.time, rect.y + secondHeight, 0));
			}
			
			var lastPoint = volumePoints[volumePoints.Count - 1];
			var lastHeight = rect.height * (1 - lastPoint.volume);
			GL.Vertex(new Vector3(rect.x + rect.width * lastPoint.time, rect.y + lastHeight, 0));
			GL.Vertex(new Vector3(rect.x + rect.width * lastPoint.time, rect.y + (rect.height * 2 - lastHeight), 0));
			GL.Vertex(new Vector3(rect.x + rect.width, rect.y + (rect.height * 2 - lastHeight), 0));
			GL.Vertex(new Vector3(rect.x + rect.width, rect.y + lastHeight, 0));
		}
		
		GL.End();
	}
	
	public void drawVolumeHandles(Rect rect, Texture texture) {
		for (var i = 0; i < volumePoints.Count; i++) {
			var point = volumePoints[i];
			EditorGUI.DrawTextureAlpha(new Rect(rect.x + rect.width * point.time - 4, rect.y + rect.height * (1 - point.volume) - 4, 8, 8), texture, ScaleMode.StretchToFill, 0, 0);
		}
	}
	
	public void drawMouseHover(Rect channelRect, Texture hoverPoint) {
		if (!channelRect.containsPoint(Event.current.mousePosition)) { return; }
		var trackPosition = Rect.PointToNormalized(channelRect, Event.current.mousePosition);
		if (trackPosition.y > 0.5f) { return; }
		
		if (movingVolumePoint.HasValue && movingVolumePoint.Value.hasStartedDragging) {
			EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 2, Event.current.mousePosition.y - 2, 4, 4), MouseCursor.MoveArrow);
			return;
		}
			
		float distance;
		var index = nearestVolumePointIndex(channelRect, Event.current.mousePosition, out distance);
		if (index != -1 && distance < 16) {
			EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 2, Event.current.mousePosition.y - 2, 4, 4), MouseCursor.ArrowMinus);
			return;
		}
		
		var linePos = mousePositionOnVolumeLine(channelRect);
		if (linePos.HasValue) {
			EditorGUI.DrawTextureAlpha(new Rect(linePos.Value.x - 4, linePos.Value.y - 4, 8, 8), hoverPoint, ScaleMode.StretchToFill, 0, 0);
			EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x - 2, Event.current.mousePosition.y - 2, 4, 4), MouseCursor.ArrowPlus);
		}
	}
	
	public int nearestVolumePointIndex(Rect channelRect, Vector2 point, out float distance) {
		var minDistance = (float?)null;
		var index = -1;
		
		for (var i = 0; i < volumePoints.Count; i++) {
			var volumePoint = Rect.NormalizedToPoint(channelRect, new Vector2(volumePoints[i].time, (1 - volumePoints[i].volume) / 2));
			
			var pointDistance = Vector2.Distance(volumePoint, point);
			if (!minDistance.HasValue || pointDistance < minDistance) {
				minDistance = pointDistance;
				index = i;
			}
		}
		
		distance = (minDistance ?? 0);
		return index;
	}
	
	public float volumeAtTime(float time) {
		if (volumePoints.Count == 0) { return 1; }
		if (volumePoints.Count == 1) { return volumePoints[0].volume; }
		
		for (var i = 0; i < volumePoints.Count; i++) {
			if (volumePoints[i].time < time) { continue; }
			
			if (i == 0) {
				return volumePoints[0].volume;
			} else {
				var progress = Mathf.InverseLerp(volumePoints[i - 1].time, volumePoints[i].time, time);
				return Mathf.Lerp(volumePoints[i - 1].volume, volumePoints[i].volume, progress);
			}
		}
		
		return volumePoints[volumePoints.Count - 1].volume;
	}
	
	public int repositionVolumePoint(int index) {
		var point = volumePoints[index];
		volumePoints.RemoveAt(index);
		
		for (var i = 0; i < volumePoints.Count; i++) {
			if (point.time < volumePoints[i].time) {
				volumePoints.Insert(i, point);
				return i;
			}
		}
		
		volumePoints.Add(point);
		return volumePoints.Count - 1;
	}
	
	public Vector2? mousePositionOnVolumeLine(Rect channelRect) {
		if (!channelRect.containsPoint(Event.current.mousePosition)) { return null; }
		var mouse = Event.current.mousePosition;
		
		if (volumePoints.Count == 0) {
			return mouse.pointOnLine(new Vector2(channelRect.x, channelRect.y), new Vector2(channelRect.x + channelRect.width, channelRect.y), maxDistance: 10);
		}
		
		if (volumePoints.Count == 1) {
			var volumePoint = Rect.NormalizedToPoint(channelRect, new Vector2(volumePoints[0].time, (1 - volumePoints[0].volume) / 2));
			return mouse.pointOnLine(new Vector2(channelRect.x, volumePoint.y), new Vector2(channelRect.x + channelRect.width, volumePoint.y), maxDistance: 10);
		}
		
		for (var i = 0; i < volumePoints.Count; i++) {
			var volumePoint = Rect.NormalizedToPoint(channelRect, new Vector2(volumePoints[i].time, (1 - volumePoints[i].volume) / 2));
			if (volumePoint.x < mouse.x) { continue; }
			
			if (i == 0) {
				var volumePointPrev = new Vector2(channelRect.x, volumePoint.y);
				return mouse.pointOnLine(volumePoint, volumePointPrev, maxDistance: 10);
			} else {
				var volumePointPrev = Rect.NormalizedToPoint(channelRect, new Vector2(volumePoints[i - 1].time, (1 - volumePoints[i - 1].volume) / 2));
				return mouse.pointOnLine(volumePoint, volumePointPrev, maxDistance: 10);
			}
		}
		
		var lastPoint = Rect.NormalizedToPoint(channelRect, new Vector2(volumePoints[volumePoints.Count - 1].time, (1 - volumePoints[volumePoints.Count - 1].volume) / 2));
		return mouse.pointOnLine(lastPoint, new Vector2(channelRect.x + channelRect.width, lastPoint.y), maxDistance: 10);
	}
	
	public int addVolumePoint(float time, float volume) {
		volumePoints.Add(new VolumePoint { time = time, volume = volume });
		return repositionVolumePoint(volumePoints.Count - 1);
	}
	
	public bool handleMouseEvent(Event e, Rect channelRect) {
		switch (e.type) {
			case EventType.MouseDown: {
				if (!channelRect.containsPoint(e.mousePosition)) { return false; }
				var trackPosition = Rect.PointToNormalized(channelRect, e.mousePosition);
				if (trackPosition.y > 0.5f) { return false; }
				
				float distance;
				var index = nearestVolumePointIndex(channelRect, Event.current.mousePosition, out distance);
				
				if (index != -1 && distance < 16) {
					movingVolumePoint = new MovingVolumePoint { index = index, startPosition = Event.current.mousePosition, hasStartedDragging = false };
				} else {
					var linePos = mousePositionOnVolumeLine(channelRect);
					if (linePos.HasValue) {
						var newIndex = addVolumePoint(time: trackPosition.x, volume: 1 - trackPosition.y * 2);
						movingVolumePoint = new MovingVolumePoint { index = newIndex, startPosition = Event.current.mousePosition, hasStartedDragging = true };
					} else {
						return false;
					}
				}
				
				return true;
			}
			case EventType.MouseDrag: {
				if (!movingVolumePoint.HasValue) { return false; }
				if (!channelRect.containsPoint(e.mousePosition)) { return false; }
				var trackPosition = Rect.PointToNormalized(channelRect, e.mousePosition);
				if (trackPosition.y > 0.5f) { return false; }
				
				var newMovingPoint = movingVolumePoint.Value;
				
				var delta = newMovingPoint.startPosition - Event.current.mousePosition;
				if (delta.magnitude > 10) { newMovingPoint.hasStartedDragging = true; }
				
				if (newMovingPoint.hasStartedDragging) {
					var point = volumePoints[newMovingPoint.index];
					point.time = trackPosition.x;
					point.volume = 1 - trackPosition.y * 2;
					volumePoints[newMovingPoint.index] = point;
					
					newMovingPoint.index = repositionVolumePoint(newMovingPoint.index);
					movingVolumePoint = newMovingPoint;
				}
					
				return true;
			}
			case EventType.MouseUp: {
				if (!movingVolumePoint.HasValue) { return false; }
				
				if (!movingVolumePoint.Value.hasStartedDragging) {
					volumePoints.RemoveAt(movingVolumePoint.Value.index);
				} else {
					removeDoubles();
				}
				
				movingVolumePoint = null;
				
				return true;
			}
		}
		
		return false;
	}
	
	public void applyFadeIn(float fromTime, float toTime) {
		var startVolume = volumeAtTime(fromTime);
		var finalVolume = volumeAtTime(toTime);
		
		for (var i = 0; i < volumePoints.Count; i++) {
			var point = volumePoints[i];
			if (point.time < fromTime || point.time > toTime) { continue; }
			
			var progress = Mathf.InverseLerp(fromTime, toTime, volumePoints[i].time);
			point.volume *= progress;
			volumePoints[i] = point;
		}
		
		addVolumePoint(time: fromTime - 0.0001f, volume: startVolume);
		addVolumePoint(time: fromTime, volume: 0);
		addVolumePoint(time: toTime, volume: finalVolume);
		
		removeDoubles();
	}
	
	public void applyFadeOut(float fromTime, float toTime) {
		var startVolume = volumeAtTime(fromTime);
		var finalVolume = volumeAtTime(toTime);
		
		for (var i = 0; i < volumePoints.Count; i++) {
			var point = volumePoints[i];
			if (point.time < fromTime || point.time > toTime) { continue; }
			
			var progress = Mathf.InverseLerp(fromTime, toTime, volumePoints[i].time);
			point.volume *= (1 - progress);
			volumePoints[i] = point;
		}
		
		addVolumePoint(time: fromTime, volume: startVolume);
		addVolumePoint(time: toTime, volume: 0);
		addVolumePoint(time: toTime + 0.0001f, volume: finalVolume);
		
		removeDoubles();
	}
	
	public void applySilence(float fromTime, float toTime) {
		var startVolume = volumeAtTime(fromTime);
		var finalVolume = volumeAtTime(toTime);
		
		for (var i = 0; i < volumePoints.Count; i++) {
			var point = volumePoints[i];
			if (point.time < fromTime || point.time > toTime) { continue; }
			
			var progress = Mathf.InverseLerp(fromTime, toTime, volumePoints[i].time);
			point.volume = 0;
			volumePoints[i] = point;
		}
		
		addVolumePoint(time: fromTime - 0.0001f, volume: startVolume);
		addVolumePoint(time: fromTime, volume: 0);
		addVolumePoint(time: toTime, volume: 0);
		addVolumePoint(time: toTime + 0.0001f, volume: finalVolume);
		
		removeDoubles();
	}
	
	void removeDoubles() {
		// TODO implement
	}
	
	public void crop(float fromTime, float toTime) {
		for (var i = 0; i < volumePoints.Count; i++) {
			var point = volumePoints[i];
			if (point.time < fromTime || point.time > toTime) {
				volumePoints.RemoveAt(i);
				i -= 1;
				continue;
			}
			
			point.time = Mathf.InverseLerp(fromTime, toTime, point.time);
			volumePoints[i] = point;
		}
	}
}
