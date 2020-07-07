using UnityEngine;
using UnityEditor;

public class SelectionRenderer {
	public delegate void SelectionChanged();
	public event SelectionChanged selectionChanged;
	
	float? mouseDown;
	float? mouseUp;
	
	public float selectionFrom => Mathf.Min(mouseDown.Value, mouseUp.Value);
	public float selectionTo => Mathf.Max(mouseDown.Value, mouseUp.Value);
	
	public bool hasSelection => mouseDown.HasValue && mouseUp.HasValue;
	public bool hasSelectionRange => hasSelection && Mathf.Abs(mouseDown.Value - mouseUp.Value) > 0.0001f;
	
	public void moveSelection(float seconds, float clipLength, bool moveFrom = true, bool moveTo = true) {
		if (moveFrom) { moveSelectionFrom(seconds, clipLength); }
		if (moveTo) { moveSelectionTo(seconds, clipLength); }
	}
	
	public void moveSelectionFrom(float seconds, float clipLength) {
		if (mouseDown.HasValue) { mouseDown = Mathf.Clamp01(mouseDown.Value + seconds / clipLength); }
		// if (mouseUp.HasValue) { mouseUp = Mathf.Clamp01(mouseUp.Value + seconds / clipLength); }
	}
	
	public void moveSelectionTo(float seconds, float clipLength) {
		// if (mouseDown.HasValue) { mouseDown = Mathf.Clamp01(mouseDown.Value + seconds / clipLength); }
		if (!mouseUp.HasValue) { mouseUp = mouseDown; }
		if (mouseUp.HasValue) { mouseUp = Mathf.Clamp01(mouseUp.Value + seconds / clipLength); }
	}
	
	public void clearCaches() {
		mouseDown = null;
		mouseUp = null;
	}
	
	public bool handleEvent(Event e, Rect rect) {
		var mouseNormalized = Rect.PointToNormalized(rect, e.mousePosition);
		
		switch (e.type) {
			case EventType.MouseDown:
				if (e.button == 1) {
					mouseDown = null;
					mouseUp = null;
					return true;
				} else if (rect.containsPoint(e.mousePosition)) {
					mouseDown = mouseNormalized.x;
					mouseUp = null;
					return true;
				}
				break;
			case EventType.MouseDrag:
				if (!rect.containsPoint(e.mousePosition)) {
					mouseDown = null;
				}
				return true;
			case EventType.MouseUp:
				mouseUp = mouseNormalized.x;
				if (selectionChanged != null) { selectionChanged(); }
				return true;
			case EventType.MouseMove:
				return true;
		}
		
		return false;
	}
	
	public void drawSelection(Rect rect, Color color) {
		if (!mouseDown.HasValue) { return; }
		drawLine(rect, at: mouseDown.Value, width: 1, color);
		
		var mouseEnd = (mouseUp.HasValue ? mouseUp.Value : Event.current.mousePosition.x / rect.width);
		var xMin = Mathf.Min(mouseEnd, mouseDown.Value) * rect.width;
		var xMax = Mathf.Max(mouseEnd, mouseDown.Value) * rect.width;
		SelectionRenderer.drawLine(rect, mouseEnd, width: 1, color: color);
		EditorGUI.DrawRect(new Rect(xMin, 0, xMax - xMin, rect.height), new Color(color.r, color.g, color.b, 0.5f));
	}
	
	public void drawHover(Rect rect, Color color) {
		if (!rect.containsPoint(Event.current.mousePosition)) { return; }
		EditorGUI.DrawRect(new Rect(Event.current.mousePosition.x, 0, 2, rect.height), color);
	}
	
	public static void drawLine(Rect rect, float at, float width, Color color) {
		EditorGUI.DrawRect(new Rect(at * rect.width, 0, width, rect.height), color);
	}
}
